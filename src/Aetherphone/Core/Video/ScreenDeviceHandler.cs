using System.Collections.Concurrent;
using Dalamud.Hooking;
using Vortice.Direct3D11;
using GfxKernel = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Aetherphone.Core.Video;

// Ported from AlphaChannel's DxHandler (Voudi, GPL-3.0). Wraps the game's own D3D11 device via
// Dalamud's UiBuilder.DeviceHandle - not a private device, see docs/video-pipeline.md section 3
// in the AlphaChannel repo, which is the answer to Stage 0's gate question - and hooks
// IDXGISwapChain::Present as the only point at which touching ImmediateContext from outside the
// game's own render thread is safe. Uses Vortice.Direct3D11 rather than SharpDX since Aetherphone
// already depends on Vortice for PhotoCaptureService's own device access.
internal sealed class ScreenDeviceHandler : IDisposable
{
    public ID3D11Device? Device { get; private set; }

    private readonly ConcurrentDictionary<string, Action> pendingRenderWork = new();

    private unsafe delegate int PresentDelegate(void* swapChain, uint syncInterval, uint flags);

    private Hook<PresentDelegate>? presentHook;

    public unsafe void Initialize()
    {
        Device = new ID3D11Device(Plugin.PluginInterface.UiBuilder.DeviceHandle);
        HookPresent();
    }

    // Queues 'work' to run once, synchronously, from inside the game's own Present call. A
    // newer call for the same key overwrites an older, not-yet-run one - only the latest frame
    // matters.
    public void RunOnRenderThread(string key, Action work) => pendingRenderWork[key] = work;

    public void CancelRenderThreadWork(string key) => pendingRenderWork.TryRemove(key, out _);

    private unsafe void HookPresent()
    {
        var swapChainPtr = (nint)GfxKernel.Device.Instance()->SwapChain->DXGISwapChain;
        var vtable = *(nint**)swapChainPtr;
        var presentAddress = vtable[8]; // IDXGISwapChain::Present

        presentHook = Plugin.InteropProvider.HookFromAddress<PresentDelegate>(presentAddress, PresentDetour);
        presentHook.Enable();
    }

    private unsafe int PresentDetour(void* swapChain, uint syncInterval, uint flags)
    {
        foreach (var key in pendingRenderWork.Keys)
        {
            if (pendingRenderWork.TryRemove(key, out var work))
            {
                try
                {
                    work();
                }
                catch (Exception exception)
                {
                    AepLog.Error($"[Video] Render-thread callback '{key}' failed: {exception}");
                }
            }
        }

        return presentHook!.Original(swapChain, syncInterval, flags);
    }

    public void Dispose()
    {
        presentHook?.Disable();
        presentHook?.Dispose();
        presentHook = null;
        pendingRenderWork.Clear();
        Device = null; // Not ours to dispose - owned by the game process.
    }
}
