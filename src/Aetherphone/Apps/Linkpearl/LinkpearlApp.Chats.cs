using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private readonly string[] chatSegmentLabels = new string[2];
    private readonly List<LinkshellEntry> roster = new();
    private string threadKey = string.Empty;
    private int chatSegment;

    private void DrawChatsTab(Rect content)
    {
        if (GuideIntents.Consume("messages.tab.direct"))
        {
            chatSegment = 0;
        }

        if (GuideIntents.Consume("messages.tab.linkshells"))
        {
            chatSegment = 1;
        }

        var scale = UiScale.Current;
        var segRowHeight = 40f * scale;
        var segRow = new Rect(new Vector2(content.Min.X + 14f * scale, content.Min.Y),
            new Vector2(content.Max.X - 14f * scale, content.Min.Y + segRowHeight));
        UiAnchors.Report("messages.tabs", segRow);
        chatSegmentLabels[0] = Loc.T(L.Messages.TabDirect);
        chatSegmentLabels[1] = Loc.T(L.Messages.TabLinkshells);
        chatSegment = SegmentStrip.Draw("messages.tabs", segRow, chatSegmentLabels, chatSegment, frameTheme);
        var body = new Rect(new Vector2(content.Min.X, segRow.Max.Y), content.Max);
        UiAnchors.Report("messages.list", body);
        if (chatSegment == 0)
        {
            DrawDirectList(body);
        }
        else
        {
            DrawLinkshellList(body);
        }
    }

    private void DrawDirectList(Rect body)
    {
        var conversations = store.Conversations;
        if (conversations.Count == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Messages.Empty), frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var index = 0; index < conversations.Count; index++)
            {
                if (ConversationRow.Draw(conversations[index], frameTheme, lodestone))
                {
                    conversations[index].MarkRead();
                    router.Push(LinkpearlRoute.Direct(conversations[index]));
                }
            }
        }
    }

    private void DrawLinkshellList(Rect body)
    {
        LinkshellDirectory.Collect(roster);
        var threads = linkshells.Threads;
        if (roster.Count == 0 && threads.Count == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Messages.LinkshellsEmpty), frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var index = 0; index < roster.Count; index++)
            {
                var entry = roster[index];
                var shell = linkshells.Find(entry.Channel);
                var label = LinkshellLabel.Of(entry.Channel,
                    shell?.Name is { Length: > 0 } stored ? stored : entry.Name);
                var action = LinkshellRow.Draw(entry.Channel, label, shell, mutes.IsMuted(entry.Channel), frameTheme);
                HandleLinkshellRow(action, entry.Channel, entry.Name);
            }

            for (var index = 0; index < threads.Count; index++)
            {
                var shell = threads[index];
                if (InRoster(shell.Channel))
                {
                    continue;
                }

                var label = LinkshellLabel.Of(shell.Channel, shell.Name);
                var action = LinkshellRow.Draw(shell.Channel, label, shell, mutes.IsMuted(shell.Channel), frameTheme);
                HandleLinkshellRow(action, shell.Channel, shell.Name);
            }
        }
    }

    private void HandleLinkshellRow(LinkshellRowAction action, LinkshellChannel channel, string name)
    {
        if (action == LinkshellRowAction.ToggleMute)
        {
            mutes.Toggle(channel);
            return;
        }

        if (action == LinkshellRowAction.Open)
        {
            OpenLinkshell(channel, name);
        }
    }

    private void OpenLinkshell(LinkshellChannel channel, string name)
    {
        var shell = linkshells.GetOrCreate(channel, name);
        shell.MarkRead();
        router.Push(LinkpearlRoute.Shell(shell));
    }

    private bool InRoster(LinkshellChannel channel)
    {
        for (var index = 0; index < roster.Count; index++)
        {
            if (roster[index].Channel.Equals(channel))
            {
                return true;
            }
        }

        return false;
    }

    private bool DrawNotificationPauseButton(in PhoneContext context)
    {
        var scale = UiScale.Current;
        return NotificationToggleButton.Draw(context.Content, scale, "messages.notifications.toggle",
            AlertSuppression.Notifications, notificationGate.Paused, context.Theme.Accent, context.Theme.TextStrong,
            context.Theme.TextMuted, Loc.T(L.Messages.ResumeNotifications), Loc.T(L.Messages.PauseNotifications));
    }

    private void DrawDirectThread(Rect area, Conversation conversation)
    {
        conversation.MarkRead();
        notifications.RemoveGroup(conversation.SendTarget);
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, conversation.Contact, backToList);
        if (DrawDeleteHistoryButton(area))
        {
            AskDeleteHistory(conversation);
        }

        OpenTellThread(conversation);
        chatThread.Draw(ThreadBody(area), frameTheme);
        DrawChatMenu(area);
    }

    private void OpenTellThread(Conversation conversation)
    {
        var key = ChatStreams.ForTell(conversation.SendTarget);
        if (string.Equals(threadKey, key, StringComparison.Ordinal))
        {
            return;
        }

        threadKey = key;
        chatThread.Open(new GameChatTarget(key, new[] { key }, new[] { GameChannels.TellKey },
            GameChannels.TellKey, conversation.SendTarget, ChatDensity.Bubbles, false));
    }

    private bool DrawDeleteHistoryButton(Rect area)
    {
        var scale = UiScale.Current;
        var center = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        var radius = 16f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center, FontAwesomeIcon.TrashAlt,
            hovered ? frameTheme.Danger : frameTheme.TextMuted, 15f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void AskDeleteHistory(Conversation conversation)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = conversation.Contact,
            Message = Loc.T(L.Messages.DeleteHistoryConfirm),
            ConfirmLabel = Loc.T(L.Messages.DeleteHistoryButton),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Confirm = () => DeleteHistory(conversation),
        });
    }

    private void DeleteHistory(Conversation conversation)
    {
        store.Remove(conversation);
        threadKey = string.Empty;
        router.Pop();
    }

    private void DrawLinkshellThread(Rect area, LinkshellThread shell)
    {
        shell.MarkRead();
        notifications.RemoveGroup(shell.Channel.Key);
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, LinkshellLabel.Of(shell.Channel, shell.Name), backToList);
        if (DrawMuteButton(area, shell.Channel))
        {
            mutes.Toggle(shell.Channel);
        }

        OpenLinkshellThread(shell.Channel);
        chatThread.Draw(ThreadBody(area), frameTheme);
        DrawChatMenu(area);
    }

    private void OpenLinkshellThread(LinkshellChannel channel)
    {
        var key = channel.IsCrossWorld ? $"cwls{channel.Slot + 1}" : $"ls{channel.Slot + 1}";
        if (string.Equals(threadKey, key, StringComparison.Ordinal))
        {
            return;
        }

        threadKey = key;
        chatThread.Open(new GameChatTarget(key, new[] { key }, new[] { key }, key, string.Empty,
            ChatDensity.Compact, false));
    }

    private bool DrawMuteButton(Rect area, LinkshellChannel channel)
    {
        var scale = UiScale.Current;
        var center = new Vector2(area.Max.X - 22f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f);
        var radius = 16f * scale;
        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var muted = mutes.IsMuted(channel);
        var hovered = UiInteract.Hover(min, max);
        var color = muted ? frameTheme.Accent : hovered ? frameTheme.TextStrong : frameTheme.TextMuted;
        ProgressRing.CenterIcon(ImGui.GetWindowDrawList(), center,
            muted ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell, color, 15f * scale);
        HoverTooltip.Show(new Rect(min, max), Loc.T(muted ? L.Messages.Unmute : L.Messages.Mute));
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static Rect ThreadBody(Rect area) =>
        new(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * UiScale.Current), area.Max);
}
