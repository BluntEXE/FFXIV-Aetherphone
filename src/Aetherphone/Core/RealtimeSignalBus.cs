using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core;

internal readonly record struct ContentRemovalSignal(string? App, string? Kind, string ContentId, string? ParentId);

internal readonly record struct ChatSignal(string? ConversationId, ChatMessageDto? Message);

internal readonly record struct CasinoSignal(string Type, string? Reason, CasinoPayload? Payload);

internal static class ContentRemovalKinds
{
    public const string Post = "post";
    public const string Comment = "comment";
    public const string Story = "story";
}

internal sealed class RealtimeSignalBus
{
    private volatile bool realtimeActive;
    private volatile Action<CallControl>? outbound;

    public event Action<ChatSignal>? ChatPinged;
    public event Action? VelvetPinged;
    public event Action? GramPinged;
    public event Action? SocialPinged;
    public event Action? MusterPinged;
    public event Action? AnnouncementsPinged;
    public event Action<ContentRemovalSignal>? ContentRemoved;
    public event Action<CasinoSignal>? CasinoReceived;
    public event Action<bool>? ConnectedChanged;

    public bool RealtimeActive => realtimeActive;

    public void SetActive(bool active)
    {
        if (realtimeActive == active)
        {
            return;
        }

        realtimeActive = active;
        ConnectedChanged?.Invoke(active);
    }

    public void PublishChat(ChatSignal signal)
    {
        ChatPinged?.Invoke(signal);
    }

    public void PublishVelvet()
    {
        VelvetPinged?.Invoke();
    }

    public void PublishGram()
    {
        GramPinged?.Invoke();
    }

    public void PublishSocial()
    {
        SocialPinged?.Invoke();
    }

    public void PublishMuster()
    {
        MusterPinged?.Invoke();
    }

    public void PublishAnnouncements()
    {
        AnnouncementsPinged?.Invoke();
    }

    public void PublishContentRemoved(ContentRemovalSignal removal)
    {
        ContentRemoved?.Invoke(removal);
    }

    // Every other app is woken with a bare ping because HTTP holds the truth. A casino room
    // cannot be: its events carry a per-room sequence that only means anything applied in
    // arrival order, and a refetch per event would both lose that proof and cost a request per
    // ball. The casino signal therefore carries the payload it arrived with.
    public void PublishCasino(CasinoSignal signal)
    {
        CasinoReceived?.Invoke(signal);
    }

    // The room session has to answer the socket (attach, detach, resync) and there is exactly
    // one socket on the phone. Binding the sender here keeps that seam on the bus every store
    // already holds, so no app ever reaches for a second connection to speak back.
    public void BindSender(Action<CallControl>? sender)
    {
        outbound = sender;
    }

    public bool TrySend(CallControl control)
    {
        var sender = outbound;
        if (sender is null || !realtimeActive)
        {
            return false;
        }

        sender(control);
        return true;
    }
}
