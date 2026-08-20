using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using GfxKernel = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using GfxScene = FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using GfxGui = FFXIVClientStructs.FFXIV.Component.GUI;
using GameControl = FFXIVClientStructs.FFXIV.Client.Game.Control;
using NumericsMatrix4x4 = System.Numerics.Matrix4x4;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Video;

internal sealed unsafe class ScreenPainter : IDisposable
{
	private const float BaseWidth = 1.0f;
	private const float BaseHeight = 0.6f;

	internal Vector3 WorldPosition;
	internal float WorldYaw;
	internal float Scale = 1.0f;

	private readonly VertexShader _vs;
	private readonly PixelShader _ps;
	private readonly SamplerState _sampler;
	private readonly RasterizerState _rasterState;
	private readonly DepthStencilState _depthState;
	private readonly Buffer _cbuf;

	private Texture2D? _texture;
	private ShaderResourceView? _srv;

	private nint _cachedColorTexture;
	private nint _cachedDepthTexture;
	private RenderTargetView? _cachedRtv;
	private DepthStencilView? _cachedDsv;

	private const int MaxUiRects = 64;

	private const int CurveSegments = 24;
	private const float Curvature = 0.12f;
	private const int VertexCount = (CurveSegments + 1) * 2;
	internal bool Visible {get; set; } = true;

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct ScreenParams
	{
		public NumericsMatrix4x4 WorldViewProj;
		public int UiRectCount;
		public float Curvature;
		private fixed float _pad[2];
		public fixed float UiRects[MaxUiRects * 4];
	}

	internal ScreenPainter()
	{
		const string hlsl = @"
			#define MAX_UI_RECTS 64
			#define CURVE_SEGMENTS 24
			cbuffer Params : register(b0)
			{
				row_major float4x4 worldViewProj;
				int uiRectCount;
				float curvature;
				float2 _pad;
				float4 uiRects[MAX_UI_RECTS]; //xy = screen pos, zw = size, in pixels
			};
			struct VOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

			VOut VS(uint id : SV_VertexID)
			{
				uint col = id / 2;
				uint row = id % 2;
				float x = -1.0 + 2.0 * (float)col / (float)CURVE_SEGMENTS;
				float y = row == 0 ? -1.0 : 1.0;
				float z = curvature * x * x; //Parabola: 0 at center, max at the edges.

				VOut o;
				o.pos = mul(float4(x, y, z, 1), worldViewProj);
				o.uv = float2((x + 1.0) * 0.5, row == 0 ? 1.0 : 0.0);
				return o;
			}

			Texture2D tex : register(t0);
			SamplerState smp : register(s0);

			float4 PS(VOut i, bool isFrontFace : SV_IsFrontFace) : SV_TARGET
			{
				for (int r = 0; r < uiRectCount; r++)
				{
					float4 rect = uiRects[r];
					if (i.pos.x >= rect.x && i.pos.x < rect.x + rect.z &&
						i.pos.y >= rect.y && i.pos.y < rect.y + rect.w)
					{
						discard;
					}
				}

				if (!isFrontFace)
				{
					return float4(0.333, 0.333, 0.333, 1); //#555555 - back of the screen, not the (mirrored) video
				}
				return tex.Sample(smp, i.uv);
			}";

		using (var vsb = ShaderBytecode.Compile(hlsl, "VS", "vs_4_0"))
		using (var psb = ShaderBytecode.Compile(hlsl, "PS", "ps_4_0"))
		{
			_vs = new VertexShader(DxHandler.Device, vsb);
			_ps = new PixelShader(DxHandler.Device, psb);
		}

		_sampler = new SamplerState(DxHandler.Device, new SamplerStateDescription
		{
			Filter = Filter.MinMagMipLinear,
			AddressU = TextureAddressMode.Clamp,
			AddressV = TextureAddressMode.Clamp,
			AddressW = TextureAddressMode.Clamp,
			ComparisonFunction = Comparison.Never,
			MinimumLod = 0,
			MaximumLod = float.MaxValue
		});

		_rasterState = new RasterizerState(DxHandler.Device, new RasterizerStateDescription
		{
			FillMode = FillMode.Solid,
			CullMode = SharpDX.Direct3D11.CullMode.None
		});

		_depthState = new DepthStencilState(DxHandler.Device, new DepthStencilStateDescription
		{
			IsDepthEnabled = true,
			DepthWriteMask = DepthWriteMask.Zero,
			DepthComparison = Comparison.GreaterEqual
		});

		_cbuf = new Buffer(DxHandler.Device, sizeof(ScreenParams), ResourceUsage.Default,
			BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

		DxHandler.OnPresent += DrawIfReady;
	}

	internal void SetTarget(Texture2D? texture)
	{
		if (ReferenceEquals(texture, _texture))
		{
			return;
		}

		_srv?.Dispose();
		_srv = null;
		_texture = texture;

		if (texture != null)
		{
			_srv = new ShaderResourceView(DxHandler.Device, texture, new ShaderResourceViewDescription
			{
				Format = texture.Description.Format,
				Dimension = ShaderResourceViewDimension.Texture2D,
				Texture2D = { MipLevels = texture.Description.MipLevels }
			});
		}
	}

	internal void SetTransform(Vector3 worldPosition, float worldYaw, float scale)
	{
		WorldPosition = worldPosition;
		WorldYaw = worldYaw;
		Scale = scale;
	}

	private void DrawIfReady()
	{
		if (!Visible)
		{
			return;
		}

		if (!TryGetSceneTargets(out nint colorTexture, out nint depthTexture, out uint targetWidth, out uint targetHeight) || _srv == null)
		{
			return;
		}

		NumericsMatrix4x4? worldViewProj = ComputeWorldViewProj();
		if (worldViewProj == null)
		{
			return;
		}

		if (colorTexture != _cachedColorTexture || _cachedRtv == null)
		{
			_cachedRtv?.Dispose();
			_cachedRtv = CreateRenderTargetView(colorTexture);
			_cachedColorTexture = colorTexture;
		}
		if (depthTexture != _cachedDepthTexture || _cachedDsv == null)
		{
			_cachedDsv?.Dispose();
			_cachedDsv = CreateDepthStencilView(depthTexture);
			_cachedDepthTexture = depthTexture;
		}

		if (_cachedRtv == null || _cachedDsv == null)
		{
			return;
		}

		RenderTargetView rtv = _cachedRtv;
		DepthStencilView dsv = _cachedDsv;

		DeviceContext ctx = DxHandler.Device!.ImmediateContext;

		RenderTargetView[] prevRtvs = ctx.OutputMerger.GetRenderTargets(1, out DepthStencilView? prevDsv);
		VertexShader? prevVs = ctx.VertexShader.Get();
		PixelShader? prevPs = ctx.PixelShader.Get();
		InputLayout? prevIl = ctx.InputAssembler.InputLayout;
		PrimitiveTopology prevTopo = ctx.InputAssembler.PrimitiveTopology;
		BlendState? prevBlend = ctx.OutputMerger.BlendState;
		DepthStencilState? prevDss = ctx.OutputMerger.DepthStencilState;
		RasterizerState? prevRs = ctx.Rasterizer.State;

		try
		{
			var p = new ScreenParams { WorldViewProj = worldViewProj.Value, Curvature = Curvature };
			p.UiRectCount = CollectUiRects(ref p);
			ScreenParams* pp = &p;
			ctx.UpdateSubresource(new SharpDX.DataBox((nint)pp), _cbuf);

			ctx.OutputMerger.SetRenderTargets(dsv, rtv);
			ctx.Rasterizer.SetViewport(0, 0, targetWidth, targetHeight, 0, 1);
			ctx.InputAssembler.InputLayout = null;
			ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
			ctx.Rasterizer.State = _rasterState;
			ctx.OutputMerger.BlendState = null;
			ctx.OutputMerger.DepthStencilState = _depthState;
			ctx.VertexShader.Set(_vs);
			ctx.VertexShader.SetConstantBuffer(0, _cbuf);
			ctx.PixelShader.Set(_ps);
			ctx.PixelShader.SetConstantBuffer(0, _cbuf);
			ctx.PixelShader.SetShaderResource(0, _srv);
			ctx.PixelShader.SetSampler(0, _sampler);
			ctx.Draw(VertexCount, 0);
			ctx.PixelShader.SetShaderResource(0, null);
		}
		finally
		{
			ctx.OutputMerger.SetRenderTargets(prevDsv, prevRtvs);
			foreach (RenderTargetView? prevRtv in prevRtvs)
			{
				prevRtv?.Dispose();
			}
			prevDsv?.Dispose();

			ctx.VertexShader.Set(prevVs); prevVs?.Dispose();
			ctx.PixelShader.Set(prevPs); prevPs?.Dispose();
			ctx.InputAssembler.InputLayout = prevIl; prevIl?.Dispose();
			ctx.InputAssembler.PrimitiveTopology = prevTopo;
			ctx.OutputMerger.BlendState = prevBlend; prevBlend?.Dispose();
			ctx.OutputMerger.DepthStencilState = prevDss; prevDss?.Dispose();
			ctx.Rasterizer.State = prevRs; prevRs?.Dispose();
		}
	}

	private static int CollectUiRects(ref ScreenParams p)
	{
		GfxGui.AtkStage* stage = GfxGui.AtkStage.Instance();
		if (stage == null || stage->RaptureAtkUnitManager == null)
		{
			return 0;
		}

		float maxWidth = stage->ScreenSize.Width * 0.9f;
		float maxHeight = stage->ScreenSize.Height * 0.9f;

		int count = 0;
		System.Span<FFXIVClientStructs.Interop.Pointer<GfxGui.AtkUnitBase>> entries = stage->RaptureAtkUnitManager->AllLoadedUnitsList.Entries;
		for (int i = 0; i < entries.Length && count < MaxUiRects; i++)
		{
			GfxGui.AtkUnitBase* unit = entries[i].Value;
			if (unit == null || !unit->IsVisible || unit->Alpha == 0 || unit->RootNode == null || unit->RootNode->Color.A == 0 || unit->WindowNode == null)
			{
				continue;
			}

			FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
			unit->RootNode->GetBounds(&bounds);

			float x = bounds.Pos1.X;
			float y = bounds.Pos1.Y;
			float width = bounds.Pos2.X - bounds.Pos1.X;
			float height = bounds.Pos2.Y - bounds.Pos1.Y;
			if (width <= 0 || height <= 0 || width >= maxWidth || height >= maxHeight)
			{
				continue;
			}

			int baseIndex = count * 4;
			p.UiRects[baseIndex + 0] = x;
			p.UiRects[baseIndex + 1] = y;
			p.UiRects[baseIndex + 2] = width;
			p.UiRects[baseIndex + 3] = height;
			count++;
		}

		return count;
	}

	private static bool TryGetSceneTargets(out nint colorTexture, out nint depthTexture, out uint width, out uint height)
	{
		colorTexture = 0;
		depthTexture = 0;
		width = 0;
		height = 0;

		GfxKernel.Device* device = GfxKernel.Device.Instance();
		if (device == null || device->SwapChain == null)
		{
			return false;
		}

		GfxKernel.Texture* backBuffer = device->SwapChain->BackBuffer;
		if (backBuffer == null)
		{
			return false;
		}

		FFXIVClientStructs.FFXIV.Client.Graphics.Render.RenderTargetManager* rtm = FFXIVClientStructs.FFXIV.Client.Graphics.Render.RenderTargetManager.Instance();
		GfxKernel.Texture* sceneDepth = rtm != null ? rtm->DepthStencil : null;
		if (rtm == null || sceneDepth == null)
		{
			return false;
		}
		if (rtm->Resolution_Width != device->SwapChain->Width || rtm->Resolution_Height != device->SwapChain->Height)
		{
			return false;
		}

		colorTexture = (nint)backBuffer->D3D11Texture2D;
		depthTexture = (nint)sceneDepth->D3D11Texture2D;
		width = device->SwapChain->Width;
		height = device->SwapChain->Height;
		return colorTexture != 0 && depthTexture != 0;
	}

	private static RenderTargetView? CreateRenderTargetView(nint texturePtr)
	{
		if (texturePtr == 0 || DxHandler.Device == null)
		{
			return null;
		}

		try
		{
			Marshal.AddRef(texturePtr);
			using var texture = new Texture2D(texturePtr);
			return new RenderTargetView(DxHandler.Device, texture);
		}
		catch (Exception exception)
		{
			AepLog.Warning($"[ScreenPainter] Could not view the scene colour target: {exception.Message}");
			return null;
		}
	}

	private static DepthStencilView? CreateDepthStencilView(nint texturePtr)
	{
		if (texturePtr == 0 || DxHandler.Device == null)
		{
			return null;
		}

		try
		{
			Marshal.AddRef(texturePtr);
			using var texture = new Texture2D(texturePtr);
			return new DepthStencilView(DxHandler.Device, texture, new DepthStencilViewDescription
			{
				Dimension = DepthStencilViewDimension.Texture2D,
				Format = DepthViewFormat(texture.Description.Format),
				Texture2D = { MipSlice = 0 },
			});
		}
		catch (Exception exception)
		{
			AepLog.Warning($"[ScreenPainter] Could not view the scene depth target: {exception.Message}");
			return null;
		}
	}

	private static Format DepthViewFormat(Format format) => format switch
	{
		Format.R32G8X24_Typeless or Format.D32_Float_S8X24_UInt => Format.D32_Float_S8X24_UInt,
		Format.R32_Typeless or Format.D32_Float => Format.D32_Float,
		Format.R24G8_Typeless or Format.D24_UNorm_S8_UInt => Format.D24_UNorm_S8_UInt,
		Format.R16_Typeless or Format.D16_UNorm => Format.D16_UNorm,
		_ => format,
	};

	private NumericsMatrix4x4? ComputeWorldViewProj()
	{
		GameControl.CameraManager* gameCameraManager = GameControl.CameraManager.Instance();
		if (gameCameraManager == null)
		{
			return null;
		}

		FFXIVClientStructs.FFXIV.Client.Game.Camera* gameCamera = gameCameraManager->GetActiveCamera();
		if (gameCamera == null)
		{
			return null;
		}

		GfxScene.Camera* camera = &gameCamera->CameraBase.SceneCamera;
		FFXIVClientStructs.FFXIV.Client.Graphics.Render.Camera* renderCamera = camera->RenderCamera;
		if (renderCamera == null)
		{
			return null;
		}

		Vector3 camPos = ToNumerics(camera->Position);
		Vector3 camLookAt = ToNumerics(camera->LookAtVector);

		NumericsMatrix4x4 view = NumericsMatrix4x4.CreateLookAt(camPos, camLookAt, Vector3.UnitY);
		NumericsMatrix4x4 proj = CreatePerspectiveFieldOfViewReversedZ(renderCamera->FoV, renderCamera->AspectRatio, renderCamera->NearPlane, renderCamera->FarPlane);

		NumericsMatrix4x4 world =
			NumericsMatrix4x4.CreateScale(BaseWidth * Scale, BaseHeight * Scale, Scale) *
			NumericsMatrix4x4.CreateFromAxisAngle(Vector3.UnitY, WorldYaw) *
			NumericsMatrix4x4.CreateTranslation(WorldPosition);

		return world * view * proj;
	}

	private static NumericsMatrix4x4 CreatePerspectiveFieldOfViewReversedZ(float fov, float aspect, float near, float far)
	{
		float yScale = 1f / MathF.Tan(fov / 2f);
		float xScale = yScale / aspect;

		return new NumericsMatrix4x4(
			xScale, 0, 0, 0,
			0, yScale, 0, 0,
			0, 0, near / (far - near), -1,
			0, 0, near * far / (far - near), 0);
	}

	private static Vector3 ToNumerics(FFXIVClientStructs.FFXIV.Common.Math.Vector3 v)
		=> Unsafe.As<FFXIVClientStructs.FFXIV.Common.Math.Vector3, Vector3>(ref v);

	public void Dispose()
	{
		DxHandler.OnPresent -= DrawIfReady;

		_srv?.Dispose();
		_cachedRtv?.Dispose();
		_cachedDsv?.Dispose();
		_cbuf.Dispose();
		_depthState.Dispose();
		_rasterState.Dispose();
		_sampler.Dispose();
		_ps.Dispose();
		_vs.Dispose();
	}
}
