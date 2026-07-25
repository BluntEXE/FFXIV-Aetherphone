using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using InteropGenerator.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Aetherphone.Core.Video;

internal enum ScreenState : byte
{
    // No Carbuncle (VideoRenderTarget.CompanionBaseId) currently tracked nearby - the
    // "nowhere to play" state the spec calls out explicitly.
    NotSummoned,
    // Companion found, but it doesn't have the screen material loaded - either the mod hasn't
    // finished applying yet, or Penumbra/the mod failed - surfaced separately via PenumbraGate.
    AwaitingMaterial,
    Ready,
}

// Ported from AlphaChannel's Core (Voudi, GPL-3.0): the companion scan, the resource-load hooks
// that swap the VFX's screen texture for our own, and the sig-scanned actor-VFX call that
// triggers it. Kept as one class deliberately, matching the reference - the hooks, the
// companion tracking, and the VFX trigger are tightly coupled in practice. (A Plush-Cushion
// -targeted pass replacing the VFX trigger with a direct material-texture hook was tried and
// reverted - back to Carbuncle itself, driven by its own bundled VFX, as the "original TV".)
internal sealed unsafe class ScreenController : IDisposable
{
    private readonly ScreenDeviceHandler deviceHandler;
    private readonly ScreenPenumbraGate penumbraGate;
    private readonly ScreenResourceStaging staging;
    private readonly Guid sessionId;

    private readonly Dictionary<uint, IGameObject> tvOwners = new();
    private readonly Dictionary<uint, IGameObject> companionOwners = new();
    private readonly Dictionary<nint, ID3D11ShaderResourceView> views = new();

    private ID3D11Texture2D? screenTexture;
    private uint activeEntityId;
    private int texCase;
    private readonly Lock screenTextureLock = new();
    private long lastCompanionLogTicks;
    private readonly Dictionary<string, long> lastLoggedTicks = new();

    private void LogThrottled(string key, string message)
    {
        var now = Environment.TickCount64;
        if (lastLoggedTicks.TryGetValue(key, out var last) && now - last < 3000)
        {
            return;
        }

        lastLoggedTicks[key] = now;
        AepLog.Info($"[Video] {message}");
    }

    private Hook<ResourceManager.Delegates.GetResourceSync>? getResourceSyncHook;
    private Hook<Texture.Delegates.InitializeContents>? textureOnLoadHook;

    private const string ActorVfxCreateSig =
        "40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8";

    private delegate nint ActorVfxCreateDelegate(string path, nint casterAddr, nint targetAddr, float scale,
        char a5, ushort a6, char a7);

    private ActorVfxCreateDelegate? actorVfxCreate;

    public ScreenState State { get; private set; } = ScreenState.NotSummoned;
    public ScreenPenumbraGate PenumbraGate => penumbraGate;
    public bool IsHookHealthy => getResourceSyncHook is { IsDisposed: false };

    private readonly Func<bool> hideNameplates;
    private const float NameplateHideRadius = 3.0f;

    public ScreenController(ScreenDeviceHandler deviceHandler, ScreenPenumbraGate penumbraGate, Guid sessionId,
        Func<bool> hideNameplates)
    {
        this.deviceHandler = deviceHandler;
        this.penumbraGate = penumbraGate;
        this.sessionId = sessionId;
        this.hideNameplates = hideNameplates;
        staging = new ScreenResourceStaging(sessionId);
    }

    public void Initialize()
    {
        deviceHandler.Initialize();
        Plugin.NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;

        var description = new Texture2DDescription
        {
            Width = VideoRenderTarget.ScreenWidth,
            Height = VideoRenderTarget.ScreenHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            CPUAccessFlags = CpuAccessFlags.None,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            MiscFlags = ResourceOptionFlags.None,
        };
        screenTexture = deviceHandler.Device!.CreateTexture2D(description);

        var vfxAddress = Plugin.SigScanner.ScanText(ActorVfxCreateSig);
        actorVfxCreate = Marshal.GetDelegateForFunctionPointer<ActorVfxCreateDelegate>(vfxAddress);

        getResourceSyncHook = Plugin.InteropProvider.HookFromAddress<ResourceManager.Delegates.GetResourceSync>(
            ResourceManager.Addresses.GetResourceSync.Value, GetResourceSyncDetour);
        textureOnLoadHook = Plugin.InteropProvider.HookFromAddress<Texture.Delegates.InitializeContents>(
            Texture.Addresses.InitializeContents.Value, TexOnLoadDetour);
        getResourceSyncHook.Enable();
    }

    // Applies the companion's TV appearance mod (distinct from "screenvfx", which carries the
    // decoded picture) and forces a redraw. Mirrors AlphaChannel's "summon" button sequence.
    public bool ApplyCompanionAppearance(out string? error)
    {
        if (!staging.TryBuildCompanionModPaths(out var paths, out error))
        {
            return false;
        }

        if (!penumbraGate.ApplyTempMod("companion", paths))
        {
            error = penumbraGate.LastError;
            return false;
        }

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is not null && companionOwners.TryGetValue(localPlayer.EntityId, out var companion))
        {
            penumbraGate.Redraw(companion.ObjectIndex);
        }

        error = null;
        return true;
    }

    // Pushes the latest decoded frame onto the screen texture. Safe to call from any thread -
    // the actual GPU write is deferred to the game's own Present call.
    public void PushFrame(byte[] bgra, int width, int height)
    {
        if (screenTexture is null || width != VideoRenderTarget.ScreenWidth ||
            height != VideoRenderTarget.ScreenHeight)
        {
            return;
        }

        var texture = screenTexture;
        deviceHandler.RunOnRenderThread("aetherstream.screen", () =>
        {
            deviceHandler.Device!.ImmediateContext.UpdateSubresource(bgra, texture, 0, (uint)(width * 4), 0);
        });
    }

    public void OnFrameworkUpdate()
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null)
        {
            return;
        }

        var hookEnabled = getResourceSyncHook is { IsDisposed: false, IsEnabled: true };
        if (hookEnabled)
        {
            ScanForCompanions(localPlayer.EntityId);
        }

        var dutyStarted = Plugin.DutyState.IsDutyStarted;
        if (dutyStarted && hookEnabled)
        {
            AepLog.Debug("[Video] Duty started - disabling screen hook and stopping playback.");
            getResourceSyncHook?.Disable();
        }
        else if (!dutyStarted && !hookEnabled && getResourceSyncHook is { IsDisposed: false })
        {
            getResourceSyncHook.Enable();
        }
    }

    // Diagnostic - lists every ObjectKind.Companion in the object table with its actual BaseId,
    // throttled to once every ~3s. Kept from an earlier BaseId investigation; harmless to leave
    // running since Carbuncle itself is ObjectKind.BattleNpc, not Companion, so this never
    // matches it - useful if a future minion-based target is tried again.
    private void LogCompanionsSeen()
    {
        var now = Environment.TickCount64;
        if (now - lastCompanionLogTicks < 3000)
        {
            return;
        }

        lastCompanionLogTicks = now;
        foreach (var obj in Plugin.ObjectTable.Where(x => x.ObjectKind is ObjectKind.Companion))
        {
            AepLog.Info($"[Video] Companion seen: name='{obj.Name}' baseId={obj.BaseId} entityId={obj.EntityId}");
        }
    }

    private void ScanForCompanions(uint localPlayerId)
    {
        var visitedTvs = new List<uint>();
        var visitedCompanions = new List<uint>();
        var foundOwn = false;

        LogCompanionsSeen();

        foreach (var item in Plugin.ObjectTable.Where(x =>
                     x is IBattleNpc && x.BaseId == VideoRenderTarget.CompanionBaseId &&
                     x.ObjectKind is ObjectKind.BattleNpc))
        {
            if (item.Address == nint.Zero)
            {
                continue;
            }

            // Character.CompanionOwnerId is general-purpose for any pet/companion-type Character,
            // not BattleNpc-specific - the same field used regardless of which target this class
            // has pointed at over time.
            var character = (Character*)item.Address;
            var ownerId = character->CompanionOwnerId;
            LogThrottled($"companion-owner:{item.EntityId}",
                $"companion='{item.Name}' ownerId={ownerId} localPlayerId={localPlayerId} match={ownerId == localPlayerId}");
            companionOwners[ownerId] = item;
            visitedCompanions.Add(ownerId);

            if (ownerId == localPlayerId)
            {
                foundOwn = true;
            }

            // HasApplied is our own bookkeeping of whether we actually asked Penumbra to apply
            // "companion" - not a guess from the rendered material.
            if (penumbraGate.HasApplied("companion"))
            {
                visitedTvs.Add(ownerId);
                CheckoutCompanion(ownerId, item);
                continue;
            }

            if (tvOwners.ContainsKey(ownerId))
            {
                // Recognized once - keep it playing until it's actually gone, to avoid a flicker
                // if the material check has a false negative for a frame.
                visitedTvs.Add(ownerId);
                CheckoutCompanion(ownerId, item);
            }
        }

        foreach (var ownerId in tvOwners.Keys.Where(id => !visitedTvs.Contains(id)).ToList())
        {
            if (activeEntityId == ownerId)
            {
                activeEntityId = 0;
            }

            tvOwners.Remove(ownerId);
        }

        foreach (var ownerId in companionOwners.Keys.Where(id => !visitedCompanions.Contains(id)).ToList())
        {
            companionOwners.Remove(ownerId);
            tvOwners.Remove(ownerId);
        }

        State = !foundOwn ? ScreenState.NotSummoned :
            tvOwners.ContainsKey(localPlayerId) ? ScreenState.Ready : ScreenState.AwaitingMaterial;
    }

    private void CheckoutCompanion(uint ownerId, IGameObject companion)
    {
        tvOwners.TryAdd(ownerId, companion);
        if (activeEntityId == ownerId)
        {
            RefreshActorVfx(Plugin.ObjectTable.LocalPlayer!.Address, companion.Address);
        }
    }

    // Ported from AlphaChannel's Core.OnNamePlateUpdate/HideNearbyNameplates (Voudi, GPL-3.0) -
    // a continuous, distance-gated hide, unlike CameraApp's transient full-hide. Reuses the same
    // NamePlateStripper CameraApp uses rather than a second copy of the strip field list.
    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!hideNameplates() || tvOwners.Count == 0)
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

            foreach (var companion in tvOwners.Values)
            {
                if (Vector3.Distance(gameObject.Position, companion.Position) <= NameplateHideRadius)
                {
                    NamePlateStripper.Strip(handler);
                    handler.VisibilityFlags = 0;
                    break;
                }
            }
        }
    }

    public void SetActive(uint entityId) => activeEntityId = entityId;

    public void ClearActive() => activeEntityId = 0;

    // Ported from AlphaChannel's Core.RemoveCompanion (Voudi, GPL-3.0). AlphaChannel doesn't
    // rely on the per-tick ObjectTable scan alone to notice a companion is gone - its own "power
    // off" control (ControlWindow.cs:539-543) clears tracking explicitly, in the same action
    // that stops video, rather than waiting for the scan to catch up (which, per the reference's
    // own design, isn't treated as reliable enough on its own for a clean "off" state). Wired to
    // the Casting tab's Stop button alongside queue.Clear().
    public void ClearCompanion(uint localPlayerId)
    {
        tvOwners.Remove(localPlayerId);
        companionOwners.Remove(localPlayerId);
        if (activeEntityId == localPlayerId)
        {
            activeEntityId = 0;
        }

        State = ScreenState.NotSummoned;
    }

    private void RefreshActorVfx(nint casterAddr, nint targetAddr)
    {
        if (!penumbraGate.HasApplied("screenvfx"))
        {
            if (!staging.TryBuildScreenPaths(out var paths, out var error))
            {
                AepLog.Warning($"[Video] {error}");
                return;
            }

            penumbraGate.ApplyTempMod("screenvfx", paths);
            return;
        }

        lock (screenTextureLock)
        {
            actorVfxCreate?.Invoke(staging.ScreenVfxGamePath, casterAddr, targetAddr, -1f, (char)0, 0, (char)0);
        }
    }

    private ResourceHandle* GetResourceSyncDetour(ResourceManager* thisPtr, ResourceCategory* category, uint* type,
        uint* hash, CStringPointer path, void* unknown, void* unkDebugPtr, uint unkDebugInt)
    {
        if (path.ToString().Contains(VideoRenderTarget.ScreenTextureGamePathFragment))
        {
            LogThrottled("resource-hook-match", $"Resource request matched screen texture path: '{path}'");
            texCase = 1;
            textureOnLoadHook!.Enable(); // Only for the duration of this one load - hooking the
                                          // texture-init path globally is unsafe and expensive.
            var ret = getResourceSyncHook!.Original(thisPtr, category, type, hash, path, unknown, unkDebugPtr,
                unkDebugInt);
            textureOnLoadHook.Disable();
            texCase = 0;
            return ret;
        }

        return getResourceSyncHook!.Original(thisPtr, category, type, hash, path, unknown, unkDebugPtr, unkDebugInt);
    }

    private bool TexOnLoadDetour(Texture* thisPtr, void* contents)
    {
        try
        {
            if (thisPtr == null)
            {
                return textureOnLoadHook!.Original(thisPtr, contents);
            }

            uint w, h;
            try
            {
                w = thisPtr->ActualWidth;
                h = thisPtr->ActualHeight;
            }
            catch (Exception)
            {
                return textureOnLoadHook!.Original(thisPtr, contents);
            }

            if (w != VideoRenderTarget.ScreenWidth || h != VideoRenderTarget.ScreenHeight || texCase != 1)
            {
                return textureOnLoadHook!.Original(thisPtr, contents);
            }

            var loaded = textureOnLoadHook!.Original(thisPtr, contents);
            AepLog.Info($"[Video] TexOnLoadDetour: texCase=1 load succeeded={loaded} width={w} height={h}");
            if (!loaded || screenTexture is null)
            {
                return loaded;
            }

            lock (screenTextureLock)
            {
                var key = (nint)thisPtr;
                if (views.TryGetValue(key, out _) && (nint)thisPtr->D3D11Texture2D == screenTexture.NativePointer)
                {
                    return loaded;
                }

                var view = deviceHandler.Device!.CreateShaderResourceView(screenTexture,
                    new ShaderResourceViewDescription
                    {
                        Format = screenTexture.Description.Format,
                        ViewDimension = ShaderResourceViewDimension.Texture2D,
                        Texture2D = { MipLevels = screenTexture.Description.MipLevels },
                    });

                views[key] = view;
                Marshal.AddRef(screenTexture.NativePointer);
                Marshal.AddRef(view.NativePointer);

                thisPtr->D3D11Texture2D = (void*)screenTexture.NativePointer;
                thisPtr->D3D11ShaderResourceView = (void*)view.NativePointer;
                AepLog.Info("[Video] TexOnLoadDetour: screen texture swap applied.");
            }

            return loaded;
        }
        catch (Exception exception)
        {
            AepLog.Error($"[Video] TexOnLoadDetour failed: {exception}");
            return false;
        }
    }

    public void Dispose()
    {
        Plugin.NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;

        var localPlayerId = Plugin.ObjectTable.LocalPlayer?.EntityId;
        if (localPlayerId is not null && tvOwners.ContainsKey(localPlayerId.Value))
        {
            penumbraGate.Redraw(Plugin.ObjectTable.LocalPlayer!.ObjectIndex);
        }

        textureOnLoadHook?.Disable();
        textureOnLoadHook?.Dispose();
        getResourceSyncHook?.Dispose();

        // Deliberately not disposing screenTexture/the cached views - they may still be part of
        // a VFX the game is actively rendering. Ported from AlphaChannel's own Core.Dispose,
        // which leaves them for the game process to clean up on exit rather than risk freeing a
        // resource mid-render.
        views.Clear();
        deviceHandler.Dispose();
        penumbraGate.Dispose();
    }
}
