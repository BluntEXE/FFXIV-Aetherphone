using System.Collections.Concurrent;
using Dalamud.Hooking;
using Dalamud.Plugin;
using D3D11 = SharpDX.Direct3D11;
using GfxKernel = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Aetherphone.Core.Video;

internal static class DxHandler
{
	internal static D3D11.Device? Device { get; private set; }

	private static readonly ConcurrentDictionary<string, Action> _pendingRenderWork = new();

	internal static event Action? OnPresent;

	private unsafe delegate int PresentDelegate(void* swapChain, uint syncInterval, uint flags);
	private static Hook<PresentDelegate>? _presentHook;

	internal static void Initialise(IDalamudPluginInterface pluginInterface)
	{
		Device = new D3D11.Device(pluginInterface.UiBuilder.DeviceHandle);

		HookPresent();
	}

	internal static void RunOnRenderThread(string key, Action work)
	{
		_pendingRenderWork[key] = work;
	}

	internal static void CancelRenderThreadWork(string key)
	{
		_pendingRenderWork.TryRemove(key, out _);
	}

	private static unsafe void HookPresent()
	{
		nint swapChainPtr = (nint)GfxKernel.Device.Instance()->SwapChain->DXGISwapChain;
		nint* vtable = *(nint**)swapChainPtr;
		nint presentAddress = vtable[8];

		_presentHook = Plugin.InteropProvider.HookFromAddress<PresentDelegate>(presentAddress, PresentDetour);
		_presentHook.Enable();
	}

	private static unsafe int PresentDetour(void* swapChain, uint syncInterval, uint flags)
	{
		foreach (string key in _pendingRenderWork.Keys)
		{
			if (_pendingRenderWork.TryRemove(key, out Action? work))
			{
				try
				{
					work();
				}
				catch (Exception e)
				{
					AepLog.Error($"[DxHandler] Render-thread callback '{key}' failed: {e}");
				}
			}
		}

		try
		{
			OnPresent?.Invoke();
		}
		catch (Exception e)
		{
			AepLog.Error($"[DxHandler] OnPresent subscriber failed: {e}");
		}

		return _presentHook!.Original(swapChain, syncInterval, flags);
	}

	public static void Dispose()
	{
		_presentHook?.Disable();
		_presentHook?.Dispose();
		_presentHook = null;
		_pendingRenderWork.Clear();
		OnPresent = null;

		Device = null;
	}
}
