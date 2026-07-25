using Dalamud.Hooking;
using Vortice.Direct3D11;
using GfxKernel = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Aetherphone.Core.Video;

// Wraps the game's own D3D11 device via Dalamud's UiBuilder.DeviceHandle - not a private device -
// and hooks IDXGISwapChain::Present as the only point at which touching ImmediateContext from
// outside the game's own render thread is safe. Reintroduced for ScreenRenderer's depth-tested
// draw call; the rest of the old Penumbra/VFX screen path this used to serve is gone for good.
internal sealed class ScreenRenderDeviceHandler : IDisposable
{
    public ID3D11Device? Device { get; private set; }

    public event Action? Present;

    private unsafe delegate int PresentDelegate(void* swapChain, uint syncInterval, uint flags);

    private Hook<PresentDelegate>? presentHook;

    public unsafe void Initialize()
    {
        try
        {
            Device = new ID3D11Device(Plugin.PluginInterface.UiBuilder.DeviceHandle);

            var swapChainPtr = (nint)GfxKernel.Device.Instance()->SwapChain->DXGISwapChain;
            var vtable = *(nint**)swapChainPtr;
            var presentAddress = vtable[8]; // IDXGISwapChain::Present

            presentHook = Plugin.InteropProvider.HookFromAddress<PresentDelegate>(presentAddress, PresentDetour);
            presentHook.Enable();
            AepLog.Info("[Video] ScreenRenderDeviceHandler Present hook installed.");
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Video] ScreenRenderDeviceHandler.Initialize failed - Present hook not installed: {exception}");
        }
    }

    private unsafe int PresentDetour(void* swapChain, uint syncInterval, uint flags)
    {
        try
        {
            Present?.Invoke();
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Video] ScreenRenderDeviceHandler.Present callback failed: {exception}");
        }

        return presentHook!.Original(swapChain, syncInterval, flags);
    }

    public void Dispose()
    {
        presentHook?.Disable();
        presentHook?.Dispose();
        presentHook = null;
        Device = null; // Not ours to dispose - owned by the game process.
    }
}
