using Aetherphone.Core;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal readonly struct GameChatTarget
{
    public readonly string Key;
    public readonly string[] Streams;
    public readonly string[] SendChannels;
    public readonly string SendChannelKey;
    public readonly string SendTarget;
    public readonly ChatDensity Density;
    public readonly bool ShowTags;

    public GameChatTarget(string key, string[] streams, string[] sendChannels, string sendChannelKey,
        string sendTarget, ChatDensity density, bool showTags)
    {
        Key = key;
        Streams = streams;
        SendChannels = sendChannels;
        SendChannelKey = sendChannelKey;
        SendTarget = sendTarget;
        Density = density;
        ShowTags = showTags;
    }
}

internal sealed class GameChatThread : IChatTranscriptInteractions, IDisposable
{
    private const long GroupWindowSeconds = 240;
    private const float ComposerHeight = 52f;
    private const float FailureHeight = 26f;
    private const string SelfId = "linkpearl.self";

    private readonly ChatStreamView view;
    private readonly ChatSend send;
    private readonly GameData gameData;
    private readonly ChatTranscript transcript = new();
    private readonly ChatEntranceTracker entrance = new();
    private readonly GameComposer composer = new();
    private readonly List<PendingSend> ghostSends = new(4);
    private readonly List<ChatEntry> ghostEntries = new(4);
    private TranscriptMessage[] mapped = Array.Empty<TranscriptMessage>();
    private int mappedCount;
    private long mappedRevision = -1;
    private GameChatTarget target;
    private string activeChannel = string.Empty;
    private string trackedKey = string.Empty;
    private bool followBottom = true;
    private bool snapToBottom;

    public GameChatThread(ChatLog log, ChatSend send, GameData gameData)
    {
        view = new ChatStreamView(log);
        this.send = send;
        this.gameData = gameData;
    }

    public Action<ChatEntry>? Context { get; set; }

    public Action<ChatEntry, ChatChunk>? Link { get; set; }

    public void Gate() => composer.Gate();

    public void Open(in GameChatTarget next)
    {
        if (string.Equals(target.Key, next.Key, StringComparison.Ordinal))
        {
            target = next;
            return;
        }

        target = next;
        activeChannel = next.SendChannelKey;
        composer.Reset();
        followBottom = true;
        snapToBottom = true;
        mappedRevision = -1;
    }

    public void Close()
    {
        composer.Reset();
        target = default;
        trackedKey = string.Empty;
    }

    public void Draw(Rect area, PhoneTheme theme)
    {
        if (target.Streams is null)
        {
            return;
        }

        var scale = UiScale.Current;
        view.Target(target.Streams);
        view.Sync();
        SyncGhosts();
        var composerBar = new Rect(new Vector2(area.Min.X, area.Max.Y - ComposerHeight * scale), area.Max);
        var failure = FirstFailure();
        var failureBlock = failure is null ? 0f : FailureHeight * scale;
        var listRect = new Rect(area.Min, new Vector2(area.Max.X, composerBar.Min.Y - failureBlock));
        if (target.Density == ChatDensity.Bubbles)
        {
            DrawBubbles(listRect, theme);
        }
        else
        {
            DrawCompact(listRect, theme);
        }

        if (failure is not null)
        {
            DrawFailure(new Rect(new Vector2(area.Min.X, listRect.Max.Y),
                new Vector2(area.Max.X, composerBar.Min.Y)), theme, failure);
        }

        var model = new GameComposerModel
        {
            Theme = theme,
            Screen = area,
            Channels = target.SendChannels,
            ActiveChannel = activeChannel.Length > 0 ? activeChannel : target.SendChannelKey,
            SendTarget = target.SendTarget,
        };
        var result = composer.Draw(composerBar, model);
        activeChannel = result.ChannelKey;
        if (!result.Submitted || !GameChannels.TryByKey(activeChannel, out var channel))
        {
            return;
        }

        if (send.Send(channel, target.SendTarget, result.Text))
        {
            composer.Clear();
            snapToBottom = true;
        }
    }

    public void OnMessageContext(string messageId)
    {
        if (Context is not { } handler)
        {
            return;
        }

        var entries = view.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Id, messageId, StringComparison.Ordinal))
            {
                handler(entries[index]);
                return;
            }
        }
    }

    public void OnLinkClick(string messageId, int target)
    {
        var entries = view.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Id, messageId, StringComparison.Ordinal))
            {
                RaiseLink(entries[index], target);
                return;
            }
        }
    }

    private void RaiseLink(ChatEntry entry, int target)
    {
        if (Link is not { } handler)
        {
            return;
        }

        var targets = ChatRuns.For(entry).Targets;
        if (target >= 0 && target < targets.Length)
        {
            handler(entry, targets[target]);
        }
    }

    public void OnQuoteClick(string messageId)
    {
    }

    public void OnReactionClick(string messageId, string token)
    {
    }

    public void Dispose() => view.Dispose();

    private void DrawCompact(Rect listRect, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var entries = view.Entries;
        entrance.Sync(target.Key, entries.Count, entries.Count > 0 ? entries[entries.Count - 1].Id : null,
            ImGui.GetIO().DeltaTime, false);
        using (AppSurface.Begin(listRect))
        {
            if (entries.Count == 0 && ghostEntries.Count == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    Loc.T(L.Messages.Empty), theme.TextMuted);
                return;
            }

            SyncFollow();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var previous = index > 0 ? entries[index - 1] : null;
                if (previous is null || !TimeText.SameLocalDay(Seconds(previous.At), Seconds(entry.At)))
                {
                    DrawDaySeparator(entry.At, theme);
                }
                else if (!Grouped(previous, entry))
                {
                    ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxs * scale));
                }

                var style = new ChatLineStyle(RailFor(entry), previous is null || !Grouped(previous, entry), false,
                    entrance.Progress(index));
                if (ChatLineView.Draw(entry, theme, style, theme.Accent, out var tapped))
                {
                    Context?.Invoke(entry);
                }

                if (tapped >= 0)
                {
                    RaiseLink(entry, tapped);
                }
            }

            for (var index = 0; index < ghostEntries.Count; index++)
            {
                var ghost = ghostEntries[index];
                ChatLineView.Draw(ghost, theme, new ChatLineStyle(RailFor(ghost), true, true), theme.Accent, out _);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }
        }
    }

    private void DrawBubbles(Rect listRect, PhoneTheme theme)
    {
        Remap(theme);
        var model = new ChatTranscriptModel
        {
            ThreadId = target.Key,
            Messages = mapped.AsSpan(0, mappedCount),
            MyUserId = SelfId,
            Accent = theme.Accent,
            Theme = theme,
            MutedInk = theme.TextMuted,
            BodyInk = theme.TextStrong,
            EmptyText = Loc.T(L.Messages.Empty),
            LoadingText = Loc.T(L.Messages.Empty),
            IsGroup = target.Streams.Length > 1 || target.SendTarget.Length == 0,
            Interactions = this,
        };
        if (snapToBottom)
        {
            transcript.RequestSnapToBottom();
            snapToBottom = false;
        }

        transcript.Draw(listRect, model);
    }

    private void Remap(PhoneTheme theme)
    {
        var entries = view.Entries;
        var required = entries.Count + ghostEntries.Count;
        if (mappedRevision == view.Revision && mappedCount == required)
        {
            return;
        }

        mappedRevision = view.Revision;
        if (mapped.Length < required)
        {
            mapped = new TranscriptMessage[Math.Max(required, 64)];
        }

        for (var index = 0; index < entries.Count; index++)
        {
            mapped[index] = Map(entries[index], theme, 0);
        }

        for (var index = 0; index < ghostEntries.Count; index++)
        {
            mapped[entries.Count + index] = Map(ghostEntries[index], theme, TranscriptFlags.Placeholder);
        }

        mappedCount = required;
    }

    private TranscriptMessage Map(ChatEntry entry, PhoneTheme theme, byte flags)
    {
        var tag = string.Empty;
        var tagTint = theme.TextMuted;
        if (target.ShowTags && GameChannels.TryByKey(entry.ChannelKey, out var channel))
        {
            tag = GameChannels.DisplayName(channel);
            tagTint = channel.Tint;
        }

        var runs = ChatRuns.For(entry);
        return new TranscriptMessage(entry.Id, entry.IsSelf ? SelfId : entry.SenderKey, entry.Text, 0,
            Seconds(entry.At), 0, 0, null, entry.AuthorName, SenderTint.Of(entry.AuthorName), flags,
            channelTag: tag, channelTint: tagTint, runs: runs.HasLinks ? runs.Runs : null);
    }

    private void SyncGhosts()
    {
        for (var index = ghostSends.Count - 1; index >= 0; index--)
        {
            var pending = ghostSends[index];
            if (!StillPending(send.Pending, pending) || pending.Failed || !Owns(pending))
            {
                ghostSends.RemoveAt(index);
                ghostEntries.RemoveAt(index);
            }
        }

        var live = send.Pending;
        for (var index = 0; index < live.Count; index++)
        {
            var pending = live[index];
            if (pending.Failed || !Owns(pending) || Tracked(pending))
            {
                continue;
            }

            ghostSends.Add(pending);
            ghostEntries.Add(new ChatEntry(0, pending.ChannelKey, LocalName(), LocalWorld(), pending.Text,
                new[] { ChatChunk.Plain(pending.Text) }, DateTime.Now, ChatEntryFlags.Self));
        }
    }

    private bool Tracked(PendingSend pending)
    {
        for (var index = 0; index < ghostSends.Count; index++)
        {
            if (ReferenceEquals(ghostSends[index], pending))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StillPending(IReadOnlyList<PendingSend> live, PendingSend entry)
    {
        for (var index = 0; index < live.Count; index++)
        {
            if (ReferenceEquals(live[index], entry))
            {
                return true;
            }
        }

        return false;
    }

    private PendingSend? FirstFailure()
    {
        var live = send.Pending;
        for (var index = 0; index < live.Count; index++)
        {
            if (live[index].Failed && Owns(live[index]))
            {
                return live[index];
            }
        }

        return null;
    }

    private void DrawFailure(Rect bar, PhoneTheme theme, PendingSend pending)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var label = Loc.T(L.Linkpearl.NotDelivered);
        var retry = Loc.T(L.Linkpearl.Retry);
        var labelSize = Typography.Measure(label, TextStyles.Caption1);
        var retrySize = Typography.Measure(retry, TextStyles.FootnoteEmphasized);
        var left = bar.Min.X + Metrics.Space.Lg * scale;
        var center = bar.Min.Y + bar.Height * 0.5f;
        AppSkin.Icon(drawList, new Vector2(left, center), FontAwesomeIcon.ExclamationCircle.ToIconString(),
            theme.Danger, 0.62f);
        Typography.Draw(drawList, new Vector2(left + 12f * scale, center - labelSize.Y * 0.5f), label, theme.Danger,
            TextStyles.Caption1);
        var retryMin = new Vector2(bar.Max.X - Metrics.Space.Lg * scale - retrySize.X, center - retrySize.Y * 0.5f);
        Typography.Draw(drawList, retryMin, retry, theme.Accent, TextStyles.FootnoteEmphasized);
        var hit = new Rect(retryMin - new Vector2(6f * scale, 6f * scale),
            retryMin + retrySize + new Vector2(6f * scale, 6f * scale));
        if (!UiInteract.HoverClick(hit.Min, hit.Max))
        {
            return;
        }

        composer.Refill(pending.Text);
        send.Discard(pending);
    }

    private void DrawDaySeparator(DateTime at, PhoneTheme theme)
    {
        var label = TimeText.DayLabel(Seconds(at));
        if (label.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var textSize = Typography.Measure(label, TextStyles.Caption1);
        var origin = ImGui.GetCursorScreenPos();
        var chipMin = new Vector2(origin.X + (available - textSize.X - 20f * scale) * 0.5f, origin.Y + 4f * scale);
        var chipMax = chipMin + new Vector2(textSize.X + 20f * scale, textSize.Y + 8f * scale);
        Squircle.Fill(drawList, chipMin, chipMax, (chipMax.Y - chipMin.Y) * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, theme.TextMuted, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, chipMax.Y + 8f * scale));
    }

    private Vector4 RailFor(ChatEntry entry)
    {
        if (!target.ShowTags || !GameChannels.TryByKey(entry.ChannelKey, out var channel))
        {
            return default;
        }

        return channel.Tint;
    }

    private bool Owns(PendingSend pending)
    {
        if (string.Equals(pending.ChannelKey, GameChannels.TellKey, StringComparison.Ordinal))
        {
            return string.Equals(pending.Target, target.SendTarget, StringComparison.OrdinalIgnoreCase);
        }

        for (var index = 0; index < target.Streams.Length; index++)
        {
            if (string.Equals(target.Streams[index], pending.ChannelKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SyncFollow()
    {
        var scale = UiScale.Current;
        if (string.Equals(trackedKey, target.Key, StringComparison.Ordinal))
        {
            followBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f * scale;
        }
        else
        {
            trackedKey = target.Key;
            followBottom = true;
        }

        if (!snapToBottom)
        {
            return;
        }

        followBottom = true;
        snapToBottom = false;
    }

    private string LocalName() => gameData.LocalPlayer?.Name.TextValue ?? string.Empty;

    private string LocalWorld() => gameData.WorldName(gameData.LocalHomeWorldId);

    private static bool Grouped(ChatEntry previous, ChatEntry entry) =>
        string.Equals(previous.SenderKey, entry.SenderKey, StringComparison.Ordinal) &&
        string.Equals(previous.ChannelKey, entry.ChannelKey, StringComparison.Ordinal) &&
        (entry.At - previous.At).TotalSeconds <= GroupWindowSeconds;

    private static long Seconds(DateTime at) => new DateTimeOffset(at).ToUnixTimeSeconds();
}
