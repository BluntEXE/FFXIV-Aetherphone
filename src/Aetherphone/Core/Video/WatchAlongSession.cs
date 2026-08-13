using System.Collections.Concurrent;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Video;

internal sealed record WatchAlongParticipant(string UserId, string Name, string World, string DisplayName,
    string? AvatarUrl, bool IsHost);

internal sealed record NearbyStream(string HostId, string Name, string World, string DisplayName);

internal sealed record PendingJoinRequest(string UserId, string Name, string World, string DisplayName);

internal sealed record QueueSuggestion(string SuggestionId, string UserId, string Name, string World,
    string DisplayName, string Url);

internal readonly record struct PendingAlert(LocString Title, LocString Body);

internal enum WatchAlongMode : byte
{
    None,
    Hosting,
    Viewing,
}

internal sealed class WatchAlongSession : IDisposable
{
    private const int CheckEveryTicks = 30;
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
    private bool lastPublishedApprovalRequired;
    private string? viewingUrl;
    private string? rejectedRemoteUrl;
    private CallControl? lastStateMessage;

    private CallControl? pendingJoinSync;
    private CallControl? pendingStateSync;
    private volatile bool pendingViewerStop;

    private readonly ConcurrentQueue<PendingAlert> pendingAlerts = new();

    private bool awaitingHostAck;

    private bool partyOpen;

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
        stream.LeftReceived += OnLeft;
        stream.StateReceived += OnState;
        stream.Ended += OnEnded;
        stream.NearbyReceived += OnNearby;
        stream.JoinRequested += OnJoinRequested;
        stream.JoinPending += OnJoinPending;
        stream.QueueSuggested += OnQueueSuggested;
        stream.QueueSuggestionResult += OnQueueSuggestionResult;
        stream.Kicked += OnKicked;
    }

    public WatchAlongMode Mode { get; private set; } = WatchAlongMode.None;
    public bool IsHosting => Mode == WatchAlongMode.Hosting;
    public bool IsViewing => Mode == WatchAlongMode.Viewing;
    public IReadOnlyList<WatchAlongParticipant> Roster { get; private set; } = Array.Empty<WatchAlongParticipant>();
    public IReadOnlyList<NearbyStream> Nearby { get; private set; } = Array.Empty<NearbyStream>();

    public VideoQueueEntry? ViewingEntry { get; private set; }

    public bool IsAwaitingApproval { get; private set; }
    public IReadOnlyList<PendingJoinRequest> PendingRequests { get; private set; } = Array.Empty<PendingJoinRequest>();

    public IReadOnlyList<QueueSuggestion> PendingQueueSuggestions { get; private set; } =
        Array.Empty<QueueSuggestion>();

    public IReadOnlyList<WatchAlongParticipant> Watching()
    {
        if (!configuration.VideoShareWatchPresence || !session.IsSignedIn)
        {
            return Array.Empty<WatchAlongParticipant>();
        }

        return Roster;
    }

    public void RequestNearbyStreams() => stream.RequestNearby(Plugin.ClientState.TerritoryType);

    public void Join(string hostId)
    {
        if (Mode == WatchAlongMode.Hosting)
        {
            StopHostingLocal();
        }

        stream.Join(hostId);
    }

    public void OpenParty()
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            Leave();
        }

        partyOpen = true;
    }

    public void ResyncNow()
    {
        if (Mode == WatchAlongMode.Viewing && lastStateMessage is { } message)
        {
            ApplyStateSync(message);
        }
    }

    public void Leave()
    {
        if (Mode == WatchAlongMode.None && !awaitingHostAck && !IsAwaitingApproval && !partyOpen)
        {
            return;
        }

        stream.Leave();
        if (Mode == WatchAlongMode.Viewing)
        {
            video.Stop();
            viewingUrl = null;
            ViewingEntry = null;
            lastStateMessage = null;
        }

        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        IsAwaitingApproval = false;
        partyOpen = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        PendingRequests = Array.Empty<PendingJoinRequest>();
        PendingQueueSuggestions = Array.Empty<QueueSuggestion>();
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
    }

    public void ApproveRequest(string userId)
    {
        stream.Approve(userId);
        RemovePendingRequest(userId);
    }

    public void SuggestQueueItem(string url)
    {
        stream.SuggestQueueItem(url, Guid.NewGuid().ToString());
    }

    public void ApproveQueueSuggestion(string suggestionId)
    {
        var suggestion = FindQueueSuggestion(suggestionId);
        if (suggestion is null)
        {
            return;
        }

        queue.Add(new VideoQueueEntry(suggestion.Url, suggestion.Url, string.Empty, null, null));
        stream.ApproveQueueSuggestion(suggestionId);
        RemoveQueueSuggestion(suggestionId);
    }

    public void DenyQueueSuggestion(string suggestionId)
    {
        stream.DenyQueueSuggestion(suggestionId);
        RemoveQueueSuggestion(suggestionId);
    }

    public void KickParticipant(string userId)
    {
        stream.Kick(userId);
    }

    private QueueSuggestion? FindQueueSuggestion(string suggestionId)
    {
        foreach (var suggestion in PendingQueueSuggestions)
        {
            if (suggestion.SuggestionId == suggestionId)
            {
                return suggestion;
            }
        }

        return null;
    }

    private void RemoveQueueSuggestion(string suggestionId)
    {
        if (PendingQueueSuggestions.Count == 0)
        {
            return;
        }

        var updated = new List<QueueSuggestion>(PendingQueueSuggestions.Count);
        foreach (var existing in PendingQueueSuggestions)
        {
            if (existing.SuggestionId != suggestionId)
            {
                updated.Add(existing);
            }
        }

        PendingQueueSuggestions = updated;
    }

    private void RemoveQueueSuggestionsByUser(string userId)
    {
        if (PendingQueueSuggestions.Count == 0)
        {
            return;
        }

        var updated = new List<QueueSuggestion>(PendingQueueSuggestions.Count);
        foreach (var existing in PendingQueueSuggestions)
        {
            if (existing.UserId != userId)
            {
                updated.Add(existing);
            }
        }

        PendingQueueSuggestions = updated;
    }

    public void DenyRequest(string userId)
    {
        stream.Deny(userId);
        RemovePendingRequest(userId);
    }

    public void OnFrameworkUpdate(float deltaSeconds)
    {
        if (Interlocked.Exchange(ref pendingJoinSync, null) is { } joinMessage)
        {
            ApplyJoinSync(joinMessage);
        }

        if (Interlocked.Exchange(ref pendingStateSync, null) is { } stateMessage)
        {
            ApplyStateSync(stateMessage);
        }

        if (pendingViewerStop)
        {
            pendingViewerStop = false;
            video.Stop();
        }

        while (pendingAlerts.TryDequeue(out var alert))
        {
            confirm.Alert(Loc.T(alert.Title), Loc.T(alert.Body), Loc.T(L.Phone.OutcomeDismiss));
        }

        if (Mode == WatchAlongMode.Viewing)
        {
            if (queue.Current is not null)
            {
                Leave();
            }

            return;
        }

        if (IsAwaitingApproval)
        {
            if (queue.Current is not null)
            {
                Leave();
            }

            return;
        }

        if (!partyOpen && queue.Current is null)
        {
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

        var progress = video.Progress;
        var position = progress.Position;
        var paused = progress.Paused;
        var url = queue.Current?.Url ?? string.Empty;

        Vector3? screenPosition = screen.Engine.IsActive ? screen.Engine.ScreenPosition : null;
        var screenYaw = screen.Engine.ScreenYaw;
        var screenScale = screen.Engine.ScreenScale;
        var screenChanged = screenPosition is { } currentScreenPosition
            && (lastPublishedScreenPosition is not { } lastScreenPosition
                || Vector3.Distance(currentScreenPosition, lastScreenPosition) > ScreenPositionDriftTolerance
                || MathF.Abs(screenYaw - lastPublishedScreenYaw) > ScreenYawDriftTolerance
                || MathF.Abs(screenScale - lastPublishedScreenScale) > ScreenScaleDriftTolerance);

        var approvalRequired = configuration.VideoStreamApprovalRequired;
        var changed = url != lastPublishedUrl || paused != lastPublishedPaused
            || Math.Abs(position - lastPublishedPosition) > PositionDriftToleranceSeconds || screenChanged
            || approvalRequired != lastPublishedApprovalRequired;
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
        lastPublishedApprovalRequired = approvalRequired;

        if (Mode != WatchAlongMode.Hosting)
        {
            awaitingHostAck = true;
        }

        stream.PublishState(url, position, paused, Plugin.ClientState.TerritoryType, approvalRequired,
            configuration.VideoStreamDiscoverable, screenPosition,
            screenPosition is not null ? screenYaw : null,
            screenPosition is not null ? screenScale : null);
    }

    private void StopHostingLocal()
    {
        Mode = WatchAlongMode.None;
        awaitingHostAck = false;
        partyOpen = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        PendingRequests = Array.Empty<PendingJoinRequest>();
        PendingQueueSuggestions = Array.Empty<QueueSuggestion>();
        lastPublishedUrl = null;
    }

    private void OnJoined(CallControl message)
    {
        Mode = WatchAlongMode.Viewing;
        IsAwaitingApproval = false;
        Roster = ToParticipants(message.Participants);

        if (message.Url is { Length: > 0 })
        {
            Interlocked.Exchange(ref pendingJoinSync, message);
        }
    }

    private static bool IsPlayableRemoteUrl(string url) => VideoEngine.ValidateURL(url, out _);

    private void WarnOnceForRejectedUrl(string url)
    {
        if (rejectedRemoteUrl == url)
        {
            return;
        }

        rejectedRemoteUrl = url;
        AepLog.Warning($"[WatchAlong] ignoring a stream url that is not a remote http(s) address: {url}");
    }

    private void ApplyJoinSync(CallControl message)
    {
        var url = message.Url!;
        if (!IsPlayableRemoteUrl(url))
        {
            WarnOnceForRejectedUrl(url);
            return;
        }

        rejectedRemoteUrl = null;
        viewingUrl = url;
        ViewingEntry = queue.CreateDisplayEntry(url);
        video.Play(url);
        ApplyRemoteScreenTransform(message);
        if (message.PositionSeconds is { } position)
        {
            video.Seek((float)position);
        }

        video.Pause(message.Paused ?? false);
    }

    private void OnDeclined(CallControl message)
    {
        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;

        if (message.Reason == "denied")
        {
            QueueAlert(L.AetherStream.JoinDeniedTitle, L.AetherStream.JoinDeniedBody);
            return;
        }

        QueueAlert(L.AetherStream.StreamUnavailableTitle, L.AetherStream.StreamUnavailableBody);
    }

    private void OnRoster(CallControl message)
    {
        Roster = ToParticipants(message.Participants);

        if (PendingRequests.Count > 0)
        {
            foreach (var participant in Roster)
            {
                RemovePendingRequest(participant.UserId);
            }
        }
    }

    private void OnLeft(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.UserId is not { } userId)
        {
            return;
        }

        RemovePendingRequest(userId);
        RemoveQueueSuggestionsByUser(userId);
    }

    private void OnJoinRequested(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.From is not { } from)
        {
            return;
        }

        var updated = new List<PendingJoinRequest>(PendingRequests.Count + 1);
        foreach (var existing in PendingRequests)
        {
            if (existing.UserId != from.UserId)
            {
                updated.Add(existing);
            }
        }

        updated.Add(new PendingJoinRequest(from.UserId, from.Name, from.World, from.DisplayName));
        PendingRequests = updated;
    }

    private void OnJoinPending(CallControl message)
    {
        if (Mode != WatchAlongMode.None)
        {
            return;
        }

        IsAwaitingApproval = true;
    }

    private void OnQueueSuggested(CallControl message)
    {
        if (Mode != WatchAlongMode.Hosting || message.From is not { } from
            || message.SuggestionId is not { Length: > 0 } suggestionId || message.Url is not { Length: > 0 } url)
        {
            return;
        }

        if (!IsPlayableRemoteUrl(url))
        {
            AepLog.Warning($"[WatchAlong] dropping a queue suggestion that is not a remote http(s) address: {url}");
            return;
        }

        var updated = new List<QueueSuggestion>(PendingQueueSuggestions.Count + 1);
        foreach (var existing in PendingQueueSuggestions)
        {
            if (existing.SuggestionId != suggestionId)
            {
                updated.Add(existing);
            }
        }

        updated.Add(new QueueSuggestion(suggestionId, from.UserId, from.Name, from.World, from.DisplayName, url));
        PendingQueueSuggestions = updated;
    }

    private void OnQueueSuggestionResult(CallControl message)
    {
        if (message.Reason == "accepted")
        {
            QueueAlert(L.AetherStream.QueueSuggestionAcceptedTitle, L.AetherStream.QueueSuggestionAcceptedBody);
            return;
        }

        QueueAlert(L.AetherStream.QueueSuggestionDeniedTitle, L.AetherStream.QueueSuggestionDeniedBody);
    }

    private void RemovePendingRequest(string userId)
    {
        if (PendingRequests.Count == 0)
        {
            return;
        }

        var updated = new List<PendingJoinRequest>(PendingRequests.Count);
        foreach (var existing in PendingRequests)
        {
            if (existing.UserId != userId)
            {
                updated.Add(existing);
            }
        }

        PendingRequests = updated;
    }

    private void OnState(CallControl message)
    {
        if (awaitingHostAck)
        {
            awaitingHostAck = false;
            Mode = WatchAlongMode.Hosting;
            return;
        }

        if (Mode == WatchAlongMode.Hosting)
        {
            return;
        }

        if (Mode != WatchAlongMode.Viewing)
        {
            return;
        }

        lastStateMessage = message;
        Interlocked.Exchange(ref pendingStateSync, message);
    }

    private void ApplyStateSync(CallControl message)
    {
        if (message.Url is { Length: > 0 } url && url != viewingUrl)
        {
            if (!IsPlayableRemoteUrl(url))
            {
                WarnOnceForRejectedUrl(url);
                return;
            }

            rejectedRemoteUrl = null;
            viewingUrl = url;
            ViewingEntry = queue.CreateDisplayEntry(url);
            video.Play(url);
        }

        if (message.PositionSeconds is { } position)
        {
            var localPosition = video.Progress.Position;
            if (Math.Abs(localPosition - position) > PositionDriftToleranceSeconds)
            {
                video.Seek((float)position);
            }
        }

        if (message.Paused is { } paused)
        {
            video.Pause(paused);
        }

        ApplyRemoteScreenTransform(message);
    }

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
            viewingUrl = null;
            ViewingEntry = null;
            lastStateMessage = null;
            pendingViewerStop = true;
        }

        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        partyOpen = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        PendingRequests = Array.Empty<PendingJoinRequest>();
        PendingQueueSuggestions = Array.Empty<QueueSuggestion>();
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);
    }

    private void OnKicked(CallControl message)
    {
        if (Mode == WatchAlongMode.Viewing)
        {
            viewingUrl = null;
            ViewingEntry = null;
            lastStateMessage = null;
            pendingViewerStop = true;
        }

        Mode = WatchAlongMode.None;
        IsAwaitingApproval = false;
        Roster = Array.Empty<WatchAlongParticipant>();
        Interlocked.Exchange(ref pendingJoinSync, null);
        Interlocked.Exchange(ref pendingStateSync, null);

        QueueAlert(L.AetherStream.KickedTitle, L.AetherStream.KickedBody);
    }

    private void QueueAlert(LocString title, LocString body) => pendingAlerts.Enqueue(new PendingAlert(title, body));

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
        stream.LeftReceived -= OnLeft;
        stream.StateReceived -= OnState;
        stream.Ended -= OnEnded;
        stream.NearbyReceived -= OnNearby;
        stream.JoinRequested -= OnJoinRequested;
        stream.JoinPending -= OnJoinPending;
        stream.QueueSuggested -= OnQueueSuggested;
        stream.QueueSuggestionResult -= OnQueueSuggestionResult;
        stream.Kicked -= OnKicked;
    }
}
