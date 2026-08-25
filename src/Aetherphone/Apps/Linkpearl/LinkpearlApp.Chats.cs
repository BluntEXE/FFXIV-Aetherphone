using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private enum ChatFilter : byte
    {
        All,
        Tells,
        Tabs,
        Unread,
    }

    private const float SearchRowHeight = 44f;
    private const float PausedBannerHeight = 34f;
    private const float SectionLabelHeight = 22f;
    private const int FilterRailMinimumRows = 4;
    private const byte MenuPopout = 0;
    private const byte MenuMarkRead = 1;
    private const byte MenuTogglePin = 2;
    private const byte MenuToggleMute = 3;
    private const byte MenuEditTab = 4;
    private const byte MenuDeleteTab = 5;
    private const byte MenuClearHistory = 6;

    private readonly ChatSearch search = new();
    private readonly ChipRail filterRail = new();
    private readonly string[] filterLabels = new string[4];
    private readonly bool[] filterActive = new bool[4];
    private readonly DropdownMenu conversationMenu = new();
    private readonly List<DropdownMenu.Item> conversationItems = new(7);
    private readonly List<byte> conversationActions = new(7);
    private string conversationMenuKey = string.Empty;
    private ChatFilter chatFilter;

    private void DrawChatsTab(Rect content)
    {
        inbox.Sync();
        var scale = UiScale.Current;
        UiAnchors.Report("messages.list", content);
        if (inbox.Count == 0 && !search.Active)
        {
            if (EmptyState.Draw(content, ui, FontAwesomeIcon.Comments, Loc.T(L.Linkpearl.EmptyTitle),
                    Loc.T(L.Linkpearl.EmptyHint), Loc.T(L.Linkpearl.StartChat)))
            {
                OpenNewChat();
            }

            return;
        }

        var pad = Metrics.Space.Lg * scale;
        var searchBar = new Rect(new Vector2(content.Min.X + pad, content.Min.Y),
            new Vector2(content.Max.X - pad, content.Min.Y + SearchRowHeight * scale));
        SearchField.Draw(searchBar, "##linkpearlSearch", Loc.T(L.Linkpearl.SearchHint), ref chatSearchQuery,
            frameTheme);
        search.Run(chatSearchQuery, inbox, chatLog);
        var top = searchBar.Max.Y;
        if (search.Active)
        {
            DrawSearchResults(new Rect(new Vector2(content.Min.X, top + Metrics.Space.Xs * scale), content.Max));
            return;
        }

        if (notificationGate.Paused)
        {
            top = DrawPausedBanner(new Rect(new Vector2(content.Min.X + pad, top + Metrics.Space.Xs * scale),
                new Vector2(content.Max.X - pad, top + Metrics.Space.Xs * scale + PausedBannerHeight * scale)));
        }

        if (inbox.Count >= FilterRailMinimumRows)
        {
            var rail = new Rect(new Vector2(content.Min.X + pad, top + Metrics.Space.Xs * scale),
                new Vector2(content.Max.X - pad, top + Metrics.Space.Xs * scale + ChipRail.RowHeight * scale));
            DrawFilterRail(rail);
            top = rail.Max.Y;
        }

        var body = new Rect(new Vector2(content.Min.X, top + Metrics.Space.Xs * scale), content.Max);
        DrawConversationList(body, scale);
    }

    private void DrawFilterRail(Rect rail)
    {
        filterLabels[0] = Loc.T(L.Linkpearl.FilterAll);
        filterLabels[1] = Loc.T(L.Linkpearl.FilterTells);
        filterLabels[2] = Loc.T(L.Linkpearl.FilterTabs);
        filterLabels[3] = Loc.T(L.Linkpearl.FilterUnread);
        for (var index = 0; index < filterActive.Length; index++)
        {
            filterActive[index] = (int)chatFilter == index;
        }

        var tapped = filterRail.Draw(rail, ui, filterLabels, filterActive, false, "messages.filters",
            ChipRail.CompactLabelPadding);
        if (tapped >= 0)
        {
            chatFilter = (ChatFilter)tapped;
        }
    }

    private float DrawPausedBanner(Rect banner)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, banner.Min, banner.Max, banner.Height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(frameTheme.Accent, 0.14f)));
        var iconCenter = new Vector2(banner.Min.X + 18f * scale, banner.Center.Y);
        AppSkin.Icon(drawList, iconCenter, FontAwesomeIcon.BellSlash.ToIconString(), frameTheme.Accent, 0.7f);
        var resume = Loc.T(L.Linkpearl.Resume);
        var resumeSize = Typography.Measure(resume, TextStyles.FootnoteEmphasized);
        var resumeCenter = new Vector2(banner.Max.X - Metrics.Space.Lg * scale - resumeSize.X * 0.5f, banner.Center.Y);
        var labelLeft = iconCenter.X + 14f * scale;
        var labelWidth = resumeCenter.X - resumeSize.X * 0.5f - Metrics.Space.Md * scale - labelLeft;
        var label = Typography.FitText(Loc.T(L.Linkpearl.NotificationsPaused), labelWidth, TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(labelLeft, banner.Center.Y - labelSize.Y * 0.5f), label,
            frameTheme.TextStrong, TextStyles.Footnote);
        if (TextButton.Draw(resumeCenter, resume, frameTheme.Accent, scale))
        {
            notificationGate.SetPaused(false);
        }

        return banner.Max.Y;
    }

    private void DrawConversationList(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            var pinned = inbox.Pinned;
            var drewPinned = false;
            for (var index = 0; index < pinned.Count; index++)
            {
                if (!Passes(pinned[index]))
                {
                    continue;
                }

                if (!drewPinned)
                {
                    DrawSectionLabel(Loc.T(L.Linkpearl.PinnedSection), scale);
                    drewPinned = true;
                }

                DrawRow(pinned[index]);
            }

            var rows = inbox.Rows;
            var drewRows = false;
            for (var index = 0; index < rows.Count; index++)
            {
                if (!Passes(rows[index]))
                {
                    continue;
                }

                if (!drewRows && drewPinned)
                {
                    DrawSectionLabel(Loc.T(L.Messages.TabChats), scale);
                }

                drewRows = true;
                DrawRow(rows[index]);
            }

            if (!drewPinned && !drewRows)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                    Loc.T(L.Linkpearl.NoFilterMatches), frameTheme.TextMuted);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawSectionLabel(string text, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var label = Loc.Culture.TextInfo.ToUpper(text);
        var size = Typography.Measure(label, TextStyles.Caption2);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + Metrics.Space.Lg * scale, origin.Y + SectionLabelHeight * scale - size.Y - 2f * scale),
            label, frameTheme.TextMuted, TextStyles.Caption2);
        ImGui.Dummy(new Vector2(0f, SectionLabelHeight * scale));
    }

    private void DrawRow(InboxRow row)
    {
        switch (InboxRowView.Draw(row, frameTheme, lodestone, true))
        {
            case InboxRowAction.Open:
                OpenConversation(row.Key);
                break;
            case InboxRowAction.Menu:
                OpenConversationMenu(row.Key, new Rect(ImGui.GetMousePos(), ImGui.GetMousePos() + new Vector2(1f, 1f)));
                break;
            case InboxRowAction.TogglePin:
                TogglePin(row);
                break;
            case InboxRowAction.ToggleMute:
                inbox.ToggleMuted(row);
                break;
        }
    }

    private bool Passes(InboxRow row) => chatFilter switch
    {
        ChatFilter.Tells => row.IsTell,
        ChatFilter.Tabs => !row.IsTell,
        ChatFilter.Unread => row.Unread > 0,
        _ => true,
    };

    private void TogglePin(InboxRow row)
    {
        if (!row.Pinned && row.Tab is not null && tabs.PinnedCount() >= TabStore.MaxPinned)
        {
            CopyToast.Show(Loc.T(L.Linkpearl.PinLimit, TabStore.MaxPinned));
            return;
        }

        inbox.TogglePinned(row);
    }

    private void DrawSearchResults(Rect body)
    {
        var scale = UiScale.Current;
        var hits = search.Hits;
        if (hits.Count == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale),
                Loc.T(L.Linkpearl.NoMatches), frameTheme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var index = 0; index < hits.Count; index++)
            {
                if (DrawHitRow(hits[index], scale))
                {
                    OpenHit(hits[index]);
                }
            }
        }
    }

    private bool DrawHitRow(ChatHit hit, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + 56f * scale));
        ImGui.Dummy(new Vector2(width, 56f * scale));
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var drawList = ImGui.GetWindowDrawList();
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X + Metrics.Space.Xs * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X - Metrics.Space.Xs * scale, row.Max.Y - 2f * scale), Metrics.Radius.Md * scale,
                ImGui.GetColorU32(Palette.WithAlpha(frameTheme.TextStrong, 0.05f)));
        }

        var left = row.Min.X + Metrics.Space.Lg * scale;
        var right = row.Max.X - Metrics.Space.Lg * scale;
        var stamp = TimeText.Short(hit.Entry.At);
        var stampSize = Typography.Measure(stamp, TextStyles.Caption2);
        Typography.Draw(drawList, new Vector2(right - stampSize.X, row.Min.Y + 9f * scale), stamp,
            frameTheme.TextMuted, TextStyles.Caption2);
        var titleWidth = right - stampSize.X - Metrics.Space.Sm * scale - left;
        Typography.Draw(drawList, new Vector2(left, row.Min.Y + 8f * scale),
            Typography.FitText(hit.Title, titleWidth, TextStyles.SubheadlineEmphasized), frameTheme.TextStrong,
            TextStyles.SubheadlineEmphasized);
        var preview = hit.Entry.AuthorName.Length > 0
            ? string.Concat(hit.Entry.AuthorName, ": ", hit.Entry.Text)
            : hit.Entry.Text;
        Typography.Draw(drawList, new Vector2(left, row.Min.Y + 29f * scale),
            Typography.FitText(preview, right - left, TextStyles.Caption1), frameTheme.TextMuted, TextStyles.Caption1);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private void OpenHit(ChatHit hit)
    {
        chatThread.Reveal(hit.Entry.Id);
        OpenConversation(hit.ConversationKey);
    }

    private void OpenConversationMenu(string key, Rect anchor)
    {
        conversationMenuKey = key;
        var row = inbox.Find(key);
        conversationMenu.Header = row is null ? string.Empty : Title(row);
        conversationMenu.Toggle("linkpearl.conversation.menu", anchor);
    }

    private void DrawConversationMenu(Rect area)
    {
        if (!conversationMenu.IsOpenFor("linkpearl.conversation.menu"))
        {
            return;
        }

        var row = inbox.Find(conversationMenuKey);
        if (row is null)
        {
            conversationMenu.Close();
            return;
        }

        conversationItems.Clear();
        conversationActions.Clear();
        var popped = popouts.IsOpen(row.Key);
        AddMenuItem(popped ? L.Linkpearl.ClosePopout : L.Linkpearl.OpenPopout,
            popped ? FontAwesomeIcon.WindowMinimize : FontAwesomeIcon.WindowRestore, MenuPopout);
        if (row.Unread > 0)
        {
            AddMenuItem(L.Linkpearl.MarkRead, FontAwesomeIcon.CheckDouble, MenuMarkRead);
        }

        AddMenuItem(row.Pinned ? L.Common.Unpin : L.Common.Pin, FontAwesomeIcon.Thumbtack, MenuTogglePin);
        AddMenuItem(row.Muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute,
            row.Muted ? FontAwesomeIcon.Bell : FontAwesomeIcon.BellSlash, MenuToggleMute);
        if (row.Tab is not null)
        {
            AddMenuItem(L.Linkpearl.EditTab, FontAwesomeIcon.SlidersH, MenuEditTab);
        }

        AddMenuItem(L.Linkpearl.ClearHistory, FontAwesomeIcon.Eraser, MenuClearHistory, true);
        if (row.Tab is not null)
        {
            AddMenuItem(L.Linkpearl.DeleteTab, FontAwesomeIcon.TrashAlt, MenuDeleteTab, true);
        }

        var clicked = conversationMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(conversationItems));
        if (clicked < 0)
        {
            return;
        }

        RunConversationAction(row, conversationActions[clicked]);
    }

    private void AddMenuItem(LocString label, FontAwesomeIcon icon, byte action, bool danger = false)
    {
        conversationItems.Add(new DropdownMenu.Item(Loc.T(label), icon.ToIconString(), danger));
        conversationActions.Add(action);
    }

    private void RunConversationAction(InboxRow row, byte action)
    {
        switch (action)
        {
            case MenuPopout:
                if (!popouts.Toggle(row.Key))
                {
                    CopyToast.Show(Loc.T(L.Linkpearl.PopoutLimit, LinkpearlPopouts.MaxWindows));
                }

                break;
            case MenuMarkRead:
                inbox.MarkRead(row);
                inbox.FlushSeen();
                notifications.RemoveGroup(row.Key);
                break;
            case MenuTogglePin:
                TogglePin(row);
                break;
            case MenuToggleMute:
                inbox.ToggleMuted(row);
                break;
            case MenuEditTab when row.Tab is { } editTab:
                OpenTabEditor(editTab);
                break;
            case MenuDeleteTab when row.Tab is { } deleteTab:
                AskDeleteTab(deleteTab);
                break;
            case MenuClearHistory:
                AskClearHistory(row);
                break;
        }
    }

    private void AskDeleteTab(ChatTab tab) =>
        confirm.Ask(new ConfirmRequest
        {
            Title = tab.Name,
            Message = Loc.T(L.Linkpearl.DeleteTabConfirm),
            ConfirmLabel = Loc.T(L.Messages.DeleteHistoryButton),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Confirm = () =>
            {
                popouts.Close(ChatInbox.KeyForTab(tab));
                tabs.Delete(tab);
                inbox.Invalidate();
                if (router.Current.Screen != LinkpearlScreen.Root)
                {
                    router.Reset();
                }
            },
        });

    private void AskClearHistory(InboxRow row) =>
        confirm.Ask(new ConfirmRequest
        {
            Title = Title(row),
            Message = Loc.T(L.Linkpearl.ClearHistoryConfirm),
            ConfirmLabel = Loc.T(L.Linkpearl.ClearHistory),
            CancelLabel = Loc.T(L.Messages.DeleteHistoryCancel),
            Confirm = () => ClearHistory(row),
        });

    private void ClearHistory(InboxRow row)
    {
        if (row.Tab is { } tab)
        {
            for (var index = 0; index < tab.Channels.Count; index++)
            {
                archive.Delete(tab.Channels[index]);
                chatLog.Clear(tab.Channels[index]);
            }
        }
        else
        {
            archive.Delete(row.StreamKey);
            chatLog.Clear(row.StreamKey);
        }

        inbox.Invalidate();
        threadKey = string.Empty;
    }

    private void OpenConversation(string key)
    {
        var row = inbox.Find(key);
        if (row is null)
        {
            return;
        }

        inbox.MarkRead(row);
        notifications.RemoveGroup(key);
        router.Push(LinkpearlRoute.Conversation(key));
    }

    private static string Title(InboxRow row) => row.Tab is { } tab ? tab.Name : row.Title;
}
