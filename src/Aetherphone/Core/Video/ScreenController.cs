using Dalamud.Game.Gui.NamePlate;

namespace Aetherphone.Core.Video;

internal enum ScreenState : byte
{
    NotReady,
    Ready,
}

// Thin read-only view over PenumbraIPC's static availability state - kept as its own type so
// call sites can keep saying `screen.PenumbraGate.IsAvailable` rather than reaching for the
// static PenumbraIPC class directly.
internal sealed class PenumbraGateStatus
{
    public bool IsAvailable => PenumbraIPC.IsAvailable;
    public string? LastError => PenumbraIPC.LastError;
}

// Adapter over VideoEngine (the ported AlphaChannel engine, Voudi, GPL-3.0) that keeps the public
// contract the rest of AetherStream depends on. The old companion-scanning ScreenController this
// replaced tracked which summoned Carbuncle belonged to which player; there is no companion at
// all anymore (see port/alphachannel-engine Stage 4), so State now just reflects whether the
// engine's resource hook is installed and running.
internal sealed class ScreenController : IDisposable
{
    private readonly Func<bool> hideNameplates;
    private const float NameplateHideRadius = 3.0f;

    internal VideoEngine Engine { get; }
    internal ScreenState State => Engine.IsHookHealthy ? ScreenState.Ready : ScreenState.NotReady;
    internal PenumbraGateStatus PenumbraGate { get; } = new();
    internal bool IsHookHealthy => Engine.IsHookHealthy;

    public ScreenController(Guid sessionId, Func<bool> hideNameplates)
    {
        this.hideNameplates = hideNameplates;
        Engine = new VideoEngine(sessionId);
        Plugin.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
    }

    public void OnFrameworkUpdate() => Engine.OnFrameworkUpdate();

    // Ported from AlphaChannel's Core.OnNamePlateUpdate/HideNearbyNameplates (Voudi, GPL-3.0),
    // distance-gated against the local player's own position now rather than a companion's -
    // the screen VFX plays at/near the local player, so that's where nameplate clutter would
    // overlap it. Reuses the same NamePlateStripper CameraApp uses.
    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!hideNameplates() || !Engine.IsActive)
        {
            return;
        }

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            var gameObject = handler.GameObject;
            if (gameObject is null)
            {
                continue;
            }

            if (Vector3.Distance(gameObject.Position, localPlayer.Position) <= NameplateHideRadius)
            {
                NamePlateStripper.Strip(handler);
                handler.VisibilityFlags = 0;
            }
        }
    }

    public void Dispose()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
        Engine.Dispose();
    }
}
