using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Aetherphone.Core.Video;

[StructLayout(LayoutKind.Sequential)]
internal struct ScreenVertex
{
    public Vector3 Position;
    public Vector2 Uv;

    public ScreenVertex(Vector3 position, Vector2 uv)
    {
        Position = position;
        Uv = uv;
    }
}

// Renders the video as an actual depth-tested triangle strip in the game's own 3D scene, from
// inside ScreenRenderDeviceHandler's Present hook - not an ImGui overlay, which has no depth buffer
// involvement at all and always draws on top of everything. This is a best-effort attempt at the
// standard technique (transform world-space verts by the game's live view-projection matrix,
// depth-test against whatever's currently bound) built without the ability to test locally - see
// the notes on TryGetViewProjection and RenderIfPlaced for the two biggest unknowns.
internal sealed unsafe class ScreenRenderer : IDisposable
{
    private const string ShaderSource = """
        cbuffer ViewProjectionBuffer : register(b0)
        {
            matrix ViewProjection;
        };

        struct VSInput
        {
            float3 Position : POSITION;
            float2 Uv : TEXCOORD0;
        };

        struct PSInput
        {
            float4 Position : SV_POSITION;
            float2 Uv : TEXCOORD0;
        };

        PSInput VSMain(VSInput input)
        {
            PSInput output;
            output.Position = mul(float4(input.Position, 1.0f), ViewProjection);
            output.Uv = input.Uv;
            return output;
        }

        Texture2D ScreenTexture : register(t0);
        SamplerState ScreenSampler : register(s0);

        float4 PSMain(PSInput input) : SV_TARGET
        {
            return ScreenTexture.Sample(ScreenSampler, input.Uv);
        }
        """;

    private readonly ScreenRenderDeviceHandler deviceHandler;
    private ID3D11VertexShader? vertexShader;
    private ID3D11PixelShader? pixelShader;
    private ID3D11InputLayout? inputLayout;
    private ID3D11Buffer? vertexBuffer;
    private ID3D11Buffer? constantBuffer;
    private ID3D11SamplerState? samplerState;
    private ID3D11DepthStencilState? depthStencilState;
    private ID3D11ShaderResourceView? textureView;
    private ID3D11Texture2D? texture;
    private int textureWidth;
    private int textureHeight;
    private bool initialized;
    private bool initFailed;

    public ScreenRenderer(ScreenRenderDeviceHandler deviceHandler)
    {
        this.deviceHandler = deviceHandler;
    }

    // Called from ScreenRenderDeviceHandler.Present, which fires from inside the game's own Present
    // call - the one place touching ImmediateContext outside the game's own render thread is
    // safe, same discipline the old ScreenController used.
    public void RenderIfPlaced(ScreenPlacement placement, byte[]? frame, int frameWidth, int frameHeight)
    {
        var device = deviceHandler.Device;
        if (!placement.IsPlaced || device is null)
        {
            return;
        }

        if (!initialized && !initFailed)
        {
            TryInitialize(device);
        }

        if (initFailed)
        {
            return; // Already logged in TryInitialize's catch.
        }

        if (vertexShader is null || pixelShader is null || vertexBuffer is null || constantBuffer is null)
        {
            LogThrottled("post-init-null",
                "init reported success but a required resource is still null - shouldn't happen");
            return;
        }

        if (frame is not null && frameWidth > 0 && frameHeight > 0)
        {
            UpdateTexture(device, frame, frameWidth, frameHeight);
        }

        if (textureView is null)
        {
            LogThrottled("no-texture", "placed but no frame decoded yet - normal if nothing is playing");
            return;
        }

        if (!TryGetViewProjection(out var viewProjection))
        {
            LogThrottled("no-view-projection", "CameraManager/CurrentCamera/RenderCamera pointer chain failed");
            return;
        }

        Draw(device, placement, viewProjection);
    }

    private readonly Dictionary<string, long> lastLoggedTicks = new();

    // Every early-return above was silent before - with zero ability to test this renderer
    // locally, that meant "doesn't show up" gave no signal at all about which of several possible
    // failure points was actually hit. Throttled per-reason so a stuck failure doesn't flood the
    // log at ~60Hz.
    private void LogThrottled(string key, string message)
    {
        var now = Environment.TickCount64;
        if (lastLoggedTicks.TryGetValue(key, out var last) && now - last < 3000)
        {
            return;
        }

        lastLoggedTicks[key] = now;
        AepLog.Warning($"[Video] ScreenRenderer {key}: {message}");
    }

    private void TryInitialize(ID3D11Device device)
    {
        initialized = true;
        try
        {
            var vsBlob = Vortice.D3DCompiler.Compiler.Compile(ShaderSource, "VSMain", "AetherStreamScreen.hlsl",
                "vs_5_0");
            var psBlob = Vortice.D3DCompiler.Compiler.Compile(ShaderSource, "PSMain", "AetherStreamScreen.hlsl",
                "ps_5_0");

            vertexShader = device.CreateVertexShader(vsBlob.Span);
            pixelShader = device.CreatePixelShader(psBlob.Span);

            var inputElements = new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
            };
            inputLayout = device.CreateInputLayout(inputElements, vsBlob.Span);

            vertexBuffer = device.CreateBuffer(new BufferDescription
            {
                ByteWidth = (uint)(sizeof(float) * 5 * 4), // 4 verts * (pos3 + uv2)
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.VertexBuffer,
                CPUAccessFlags = CpuAccessFlags.Write,
            });

            constantBuffer = device.CreateBuffer(new BufferDescription
            {
                ByteWidth = sizeof(float) * 16,
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CPUAccessFlags = CpuAccessFlags.Write,
            });

            samplerState = device.CreateSamplerState(SamplerDescription.LinearClamp);

            // Standard (non-reversed) depth convention assumed - LessEqual against whatever's
            // already in the bound depth buffer. If the screen renders fully occluded or never
            // occluded regardless of distance, this comparison is inverted for this renderer -
            // flip to GreaterEqual.
            depthStencilState = device.CreateDepthStencilState(new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.LessEqual,
            });

            AepLog.Info("[Video] ScreenRenderer initialized (shaders compiled, buffers/states created).");
        }
        catch (Exception exception)
        {
            initFailed = true;
            AepLog.Error($"[Video] ScreenRenderer init failed: {exception}");
        }
    }

    private void UpdateTexture(ID3D11Device device, byte[] frame, int width, int height)
    {
        if (texture is null || width != textureWidth || height != textureHeight)
        {
            textureView?.Dispose();
            texture?.Dispose();

            texture = device.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ShaderResource,
            });
            textureView = device.CreateShaderResourceView(texture);
            textureWidth = width;
            textureHeight = height;
        }

        device.ImmediateContext.UpdateSubresource(frame, texture, 0, (uint)(width * 4), 0);
    }

    // The single biggest unknown in this whole approach - both the exact FFXIVClientStructs
    // struct layout beyond field names (verified only by decompiling the compiled interop DLL,
    // not by reading source) and the row-vector/column-major convention this assumes. Transposes
    // before upload on the assumption these matrices are stored row-major and the shader's default
    // cbuffer packing is column-major - the standard DirectX gotcha. If the screen renders wildly
    // distorted or not at all despite everything else working, this is the first thing to revisit.
    private static bool TryGetViewProjection(out Matrix4x4 viewProjection)
    {
        viewProjection = Matrix4x4.Identity;
        var cameraManager = CameraManager.Instance();
        if (cameraManager is null)
        {
            return false;
        }

        var camera = cameraManager->CurrentCamera;
        if (camera is null || camera->RenderCamera is null)
        {
            return false;
        }

        var renderCamera = camera->RenderCamera;
        var view = *(Matrix4x4*)&renderCamera->ViewMatrix;
        var projection = *(Matrix4x4*)&renderCamera->ProjectionMatrix;
        viewProjection = Matrix4x4.Transpose(view * projection);
        return true;
    }

    private void Draw(ID3D11Device device, ScreenPlacement placement, Matrix4x4 viewProjection)
    {
        var context = device.ImmediateContext;

        // The actual answer to "is there a depth buffer to test against right now" - our own
        // OMSetDepthStencilState only controls test behavior, it says nothing about whether a
        // depth-stencil view is currently bound as a render target. If this is null, depth
        // testing is a no-op regardless of what state we set, and Present is the wrong hook point
        // - drawing would need to move to wherever the game still has its real depth view bound.
        context.OMGetRenderTargets(0, Array.Empty<ID3D11RenderTargetView>(), out var currentDepthView);
        if (currentDepthView is null)
        {
            LogThrottled("no-depth-view",
                "no depth-stencil view bound at Present - depth testing cannot work from this hook point");
            return;
        }

        currentDepthView.Dispose();

        var (topLeft, topRight, bottomRight, bottomLeft) = placement.ComputeCorners();
        Span<ScreenVertex> verts = stackalloc ScreenVertex[4]
        {
            new ScreenVertex(topLeft, new Vector2(0f, 0f)),
            new ScreenVertex(topRight, new Vector2(1f, 0f)),
            new ScreenVertex(bottomLeft, new Vector2(0f, 1f)),
            new ScreenVertex(bottomRight, new Vector2(1f, 1f)),
        };

        var mapped = context.Map(vertexBuffer!, 0, MapMode.WriteDiscard);
        verts.CopyTo(new Span<ScreenVertex>((void*)mapped.DataPointer, 4));
        context.Unmap(vertexBuffer!, 0);

        var mappedCb = context.Map(constantBuffer!, 0, MapMode.WriteDiscard);
        *(Matrix4x4*)mappedCb.DataPointer = viewProjection;
        context.Unmap(constantBuffer!, 0);

        // Saved/restored around the draw call so we don't leave the pipeline in a state the
        // game's own next draw call doesn't expect.
        using var savedVs = context.VSGetShader();
        using var savedPs = context.PSGetShader();
        using var savedLayout = context.IAGetInputLayout();
        var savedTopology = context.IAGetPrimitiveTopology();
        context.OMGetDepthStencilState(out var savedDepthState, out var savedStencilRef);
        var savedVsCb = new ID3D11Buffer[1];
        context.VSGetConstantBuffers(0, savedVsCb);
        var savedPsTexture = new ID3D11ShaderResourceView[1];
        context.PSGetShaderResources(0, savedPsTexture);
        var savedSampler = new ID3D11SamplerState[1];
        context.PSGetSamplers(0, savedSampler);

        context.IASetInputLayout(inputLayout);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);
        context.IASetVertexBuffer(0, vertexBuffer, (uint)(sizeof(float) * 5));
        context.VSSetShader(vertexShader);
        context.VSSetConstantBuffer(0, constantBuffer);
        context.PSSetShader(pixelShader);
        context.PSSetShaderResource(0, textureView);
        context.PSSetSampler(0, samplerState);
        context.OMSetDepthStencilState(depthStencilState, 0);

        context.Draw(4u, 0u);

        context.IASetInputLayout(savedLayout);
        context.IASetPrimitiveTopology(savedTopology);
        context.VSSetShader(savedVs);
        context.VSSetConstantBuffers(0, savedVsCb);
        context.PSSetShader(savedPs);
        context.PSSetShaderResources(0, savedPsTexture);
        context.PSSetSamplers(0, savedSampler);
        context.OMSetDepthStencilState(savedDepthState, savedStencilRef);

        savedDepthState?.Dispose();
        savedVsCb[0]?.Dispose();
        savedPsTexture[0]?.Dispose();
        savedSampler[0]?.Dispose();
    }

    public void Dispose()
    {
        textureView?.Dispose();
        texture?.Dispose();
        depthStencilState?.Dispose();
        samplerState?.Dispose();
        constantBuffer?.Dispose();
        vertexBuffer?.Dispose();
        inputLayout?.Dispose();
        pixelShader?.Dispose();
        vertexShader?.Dispose();
    }
}
