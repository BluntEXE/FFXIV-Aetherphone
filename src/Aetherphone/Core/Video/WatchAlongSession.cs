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

    private readonly AethernetSession session;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private readonly AetherStreamQueue queue;
    private readonly StreamSignalRouter stream;

    private int tickCounter;
    private float heartbeatTimer;
    private string? lastPublishedUrl;
    private double lastPublishedPosition;
    private bool lastPublishedPaused;
    private string? viewingUrl;

    public WatchAlongSession(AethernetSession session, Configuration configuration, ConfirmService confirm,
        VideoPlayer video, ScreenController screen, AetherStreamQueue queue, StreamSignalRouter stream)
    {
        this.session = session;
        this.configuration = configuration;
        this.confirm = confirm;
        this.video = video;
        this.screen = screen;
        this.queue = queue;
        this.stream = stream;
        stream.Joined += OnJoined;
        stream.Declined += OnDeclined;
        stream.RosterReceived += OnRoster;
        stream.StateReceived += OnState;
        stream.Ended += OnEnded;
    }

    public WatchAlongMode Mode { get; private set; } = WatchAlongMode.None;
    public bool IsHosting => Mode == WatchAlongMode.Hosting;
    public bool IsViewing => Mode == WatchAlongMode.Viewing;
    public IReadOnlyList<WatchAlongParticipant> Roster { get; private set; } = Array.Empty<WatchAlongParticipant>();

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
        if (Mode == WatchAlongMode.None)
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
            if (Mode == WatchAlongMode.Hosting)
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
        var changed = url != lastPublishedUrl || paused != lastPublishedPaused
            || Math.Abs(position - lastPublishedPosition) > PositionDriftToleranceSeconds;
        var heartbeatDue = heartbeatTimer >= HeartbeatSeconds;
        if (!changed && !heartbeatDue)
        {
            return;
        }

        heartbeatTimer = 0f;
        lastPublishedUrl = url;
        lastPublishedPosition = position;
        lastPublishedPaused = paused;
        Mode = WatchAlongMode.Hosting;
        stream.PublishState(url, position, paused);
    }

    private void StopHostingLocal()
    {
        Mode = WatchAlongMode.None;
        Roster = Array.Empty<WatchAlongParticipant>();
        lastPublishedUrl = null;
    }

    private void OnJoined(CallControl message)
    {
        Mode = WatchAlongMode.Viewing;
        Roster = ToParticipants(message.Participants);

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is not null)
        {
            screen.SetActive(localPlayer.EntityId);
        }

        if (message.Url is { Length: > 0 } url)
        {
            viewingUrl = url;
            video.Play(url);
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
        if (Mode == WatchAlongMode.Hosting)
        {
            // Our own echo - the live-ack. Nothing else to do; publishing already reflects this.
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
    }
}
