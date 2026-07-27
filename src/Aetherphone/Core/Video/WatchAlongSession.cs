using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

// Shape mirrors Telephony's ParticipantInfo - kept distinct so the video layer never needs to
// know about Telephony's contracts directly. IsHost is decided from the server's own slot 0
// convention (see StreamSignalRouter), not fabricated locally.
internal sealed record WatchAlongParticipant(string UserId, string Name, string World, string DisplayName,
    string? AvatarUrl, bool IsHost);

// Shape mirrors Telephony's NearbyStreamInfo, same reasoning as WatchAlongParticipant above.
internal sealed record NearbyStream(string HostId, string Name, string World, string DisplayName);

internal enum WatchAlongMode : byte
{
    None,
    Hosting,
    Viewing,
}

// Real cross-player watch-along, built on StreamSignalRouter's stream.* signals (server-side as
// of the dev-branch deploy - see the spec that shipped this file). A connection is host XOR
// viewer, never both, mirroring the server's own "joining ends your own stream" policy - Mode
// tracks that locally so the UI and the framework tick agree on which role is active.
internal sealed class WatchAlongSession : IDisposable
{
    private const int CheckEveryTicks = 30; // ~2x/sec at 60fps, matches AetherStreamQueue's own throttle
    private const float HeartbeatSeconds = 8f;
    private const double PositionDriftToleranceSeconds = 3.0;

    private const float ScreenPositionDriftTolerance = 0.1f;
    private const float ScreenYawDriftTolerance = 0.02f;
    private const float ScreenScaleDriftTolerance = 0.02f;

    private readonly AethernetSession session;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly VideoPlayer video;
    private readonly AetherStreamQueue queue;
    private readonly StreamSignalRouter stream;
    private readonly ScreenController screen;

    private int tickCounter;
    private float heartbeatTimer;
    private string? lastPublishedUrl;
    private double lastPublishedPosition;
    private bool lastPublishedPaused;
    private Vector3? lastPublishedScreenPosition;
    private float lastPublishedScreenYaw;
    private float lastPublishedScreenScale;
    private string? viewingUrl;

    // Per the protocol spec: "the host's FIRST stream.state creates the room ('go live') ... wait
    // for that echo before treating yourself as hosting." Set right before the first PublishState
    // call of a session, cleared the moment that echo comes back through OnState. Mode only
    // becomes Hosting once the server has actually confirmed it, never optimistically.
    private bool awaitingHostAck;

    public WatchAlongSession(AethernetSession session, Configuration configuration, ConfirmService confirm,
        VideoPlayer video, AetherStreamQueue queue, StreamSignalRouter stream, ScreenController screen)
    {
        this.session = session;
        this.configuration = configuration;
        this.confirm = confirm;
        this.video = video;
        this.queue = queue;
        this.stream = stream;
        this.screen = screen;
        stream.Joined += OnJoined;
        stream.Declined += OnDeclined;
        stream.RosterReceived += OnRoster;
        stream.StateReceived += OnState;
        stream.Ended += OnEnded;
        stream.NearbyReceived += OnNearby;
    }

    public WatchAlongMode Mode { get; private set; } = WatchAlongMode.None;
    public bool IsHosting => Mode == WatchAlongMode.Hosting;
    public bool IsViewing => Mode == WatchAlongMode.Viewing;
    public IReadOnlyList<WatchAlongParticipant> Roster { get; private set; } = Array.Empty<WatchAlongParticipant>();
    public IReadOnlyList<NearbyStream> Nearby { get; private set; } = Array.Empty<NearbyStream>();

    // Only meaningful while sharing is on and signed in - showing a roster from a room the local
    // player has no visibility into, or while there's no identity to attribute it to, would just
    // be misleading chrome. Mirrors the same VideoShareWatchPresence gate documented on the
    // Settings toggle that introduced it.
    public IReadOnlyList<WatchAlongParticipant> Watching()
    {
        if (!configuration.VideoShareWatchPresence || !session.IsSignedIn)
        {
            return Array.Empty<WatchAlongParticipant>();
        }

        return Roster;
    }

    // Zone-scoped discovery - unlike Join, this needs no hostId and no mutual-contact relationship;
    // the server answers with whoever is currently hosting (see OnFrameworkUpdate's PublishState
    // call below) in the caller's own current territory. Response lands in Nearby via OnNearby.
    public void RequestNearbyStreams() => stream.RequestNearby(Plugin.ClientState.TerritoryType);

    public void Join(string hostId)
    {
        if (Mode == WatchAlongMode.Hosting)
        {
            // The server would end our own room anyway once the join lands - reflect that
            // locally right away instead of waiting for the stream.ended round-trip.
            StopHostingLocal();
        }

        stream.Join(hostId);
    }

    public void Leave()
    {
        if (Mode == WatchAlongMode.None && !awaitingHostAck)
        {
            return;
        }

        stream.Leave();
        if (Mode == WatchAlongMode.Viewing)
        {
            video.Stop();
            viewingUrl = null;
        }

        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        Roster = Array.Empty<WatchAlongParticipant>();
    }

    public void OnFrameworkUpdate(float deltaSeconds)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            // The user's own queue only ever gets an entry through explicit local action (URL
            // entry, local file, queue advance) - that always wins over a joined stream's
            // mirrored playback, so treat it as the user choosing to leave.
            if (queue.Current is not null)
            {
                Leave();
            }

            return;
        }

        if (!configuration.VideoShareWatchPresence || queue.Current is null)
        {
            // awaitingHostAck also needs a Leave() here, not just Mode == Hosting - the server
            // creates the room off our very first stream.state, before the echo (and thus our
            // local Mode flip) ever arrives, so a room can already exist server-side even while
            // we're still technically "None" from our own point of view.
            if (Mode == WatchAlongMode.Hosting || awaitingHostAck)
            {
                Leave();
            }

            return;
        }

        heartbeatTimer += deltaSeconds;
        tickCounter++;
        if (tickCounter < CheckEveryTicks)
        {
            return;
        }

        tickCounter = 0;

        var (position, _, paused) = video.GetProgress();
        var url = queue.Current.Url;

        // Only meaningful to publish while the host's own screen is actually up - an idle engine's
        // ScreenPosition is just whatever it was last left at, not a real placement.
        Vector3? screenPosition = screen.Engine.IsActive ? screen.Engine.ScreenPosition : null;
        var screenYaw = screen.Engine.ScreenYaw;
        var screenScale = screen.Engine.ScreenScale;
        var screenChanged = screenPosition is { } currentScreenPosition
            && (lastPublishedScreenPosition is not { } lastScreenPosition
                || Vector3.Distance(currentScreenPosition, lastScreenPosition) > ScreenPositionDriftTolerance
                || MathF.Abs(screenYaw - lastPublishedScreenYaw) > ScreenYawDriftTolerance
                || MathF.Abs(screenScale - lastPublishedScreenScale) > ScreenScaleDriftTolerance);

        var changed = url != lastPublishedUrl || paused != lastPublishedPaused
            || Math.Abs(position - lastPublishedPosition) > PositionDriftToleranceSeconds || screenChanged;
        var heartbeatDue = heartbeatTimer >= HeartbeatSeconds;
        if (!changed && !heartbeatDue)
        {
            return;
        }

        heartbeatTimer = 0f;
        lastPublishedUrl = url;
        lastPublishedPosition = position;
        lastPublishedPaused = paused;
        lastPublishedScreenPosition = screenPosition;
        lastPublishedScreenYaw = screenYaw;
        lastPublishedScreenScale = screenScale;

        // Only the very first publish of a session needs to wait for the ack - once we're already
        // confirmed Hosting, later updates are just that, updates, not another "go live" moment.
        if (Mode != WatchAlongMode.Hosting)
        {
            awaitingHostAck = true;
        }

        stream.PublishState(url, position, paused, Plugin.ClientState.TerritoryType, screenPosition,
            screenPosition is not null ? screenYaw : null, screenPosition is not null ? screenScale : null);
    }

    private void StopHostingLocal()
    {
        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        lastPublishedUrl = null;
    }

    private void OnJoined(CallControl message)
    {
        Mode = WatchAlongMode.Viewing;
        Roster = ToParticipants(message.Participants);

        if (message.Url is { Length: > 0 } url)
        {
            viewingUrl = url;
            video.Play(url); // Spawns the screen in front of the local player first (a new session) -
            ApplyRemoteScreenTransform(message); // then immediately re-place it at the host's spot, if sent.
            if (message.PositionSeconds is { } position)
            {
                video.Seek((float)position);
            }

            video.Pause(message.Paused ?? false);
        }
    }

    private void OnDeclined(CallControl message)
    {
        Mode = WatchAlongMode.None;
        // Deliberately generic - a decline must never reveal whether it was a block, a full room,
        // or the host simply isn't live, per the server's own policy note.
        confirm.Alert(Loc.T(L.AetherStream.StreamUnavailableTitle), Loc.T(L.AetherStream.StreamUnavailableBody),
            Loc.T(L.Phone.OutcomeDismiss));
    }

    private void OnRoster(CallControl message)
    {
        Roster = ToParticipants(message.Participants);
    }

    private void OnState(CallControl message)
    {
        if (awaitingHostAck)
        {
            // The live-ack for our own first publish - this is what actually makes us "hosting"
            // per the protocol spec ("wait for that echo before treating yourself as hosting"),
            // not the optimistic PublishState call itself.
            awaitingHostAck = false;
            Mode = WatchAlongMode.Hosting;
            return;
        }

        if (Mode == WatchAlongMode.Hosting)
        {
            // A later echo of our own update - nothing else to do; publishing already reflects this.
            return;
        }

        if (Mode != WatchAlongMode.Viewing)
        {
            return;
        }

        if (message.Url is { Length: > 0 } url && url != viewingUrl)
        {
            viewingUrl = url;
            video.Play(url);
        }

        if (message.PositionSeconds is { } position)
        {
            var (localPosition, _, _) = video.GetProgress();
            if (Math.Abs(localPosition - position) > PositionDriftToleranceSeconds)
            {
                video.Seek((float)position);
            }
        }

        if (message.Paused is { } paused)
        {
            video.Pause(paused);
        }

        // Live re-placement while already watching - lets a host who nudges their screen mid-stream
        // (Casting tab sliders, a Recenter tap, a preset) carry viewers along with them.
        ApplyRemoteScreenTransform(message);
    }

    // Shared by OnJoined/OnState - a no-op if the host didn't send a screen transform (their own
    // screen isn't active), matching CallControl's ScreenX/Y/Z/Yaw/Scale doc comment.
    private void ApplyRemoteScreenTransform(CallControl message)
    {
        if (message is { ScreenX: { } x, ScreenY: { } y, ScreenZ: { } z })
        {
            screen.Engine.ApplyRemoteScreenTransform(new Vector3(x, y, z), message.ScreenYaw ?? 0f,
                message.ScreenScale ?? 1f);
        }
    }

    private void OnNearby(CallControl message)
    {
        var streams = message.NearbyStreams;
        if (streams is null || streams.Length == 0)
        {
            Nearby = Array.Empty<NearbyStream>();
            return;
        }

        var result = new NearbyStream[streams.Length];
        for (var index = 0; index < streams.Length; index++)
        {
            var entry = streams[index];
            result[index] = new NearbyStream(entry.HostId, entry.Name, entry.World, entry.DisplayName);
        }

        Nearby = result;
    }

    private void OnEnded(CallControl message)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            video.Stop();
            viewingUrl = null;
        }

        Mode = WatchAlongMode.None;
        Roster = Array.Empty<WatchAlongParticipant>();
    }

    private static WatchAlongParticipant[] ToParticipants(ParticipantInfo[]? participants)
    {
        if (participants is null || participants.Length == 0)
        {
            return Array.Empty<WatchAlongParticipant>();
        }

        var result = new WatchAlongParticipant[participants.Length];
        for (var index = 0; index < participants.Length; index++)
        {
            var participant = participants[index];
            result[index] = new WatchAlongParticipant(participant.UserId, participant.Name, participant.World,
                participant.DisplayName, null, IsHost: participant.Slot == 0);
        }

        return result;
    }

    public void Dispose()
    {
        stream.Joined -= OnJoined;
        stream.Declined -= OnDeclined;
        stream.RosterReceived -= OnRoster;
        stream.StateReceived -= OnState;
        stream.Ended -= OnEnded;
        stream.NearbyReceived -= OnNearby;
    }
}
