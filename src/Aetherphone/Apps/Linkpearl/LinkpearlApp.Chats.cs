using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const float RailHeight = 78f;
    private const float PinDiameter = 48f;
    private const float PinSpacing = 62f;

    private static readonly TabPreset[] Presets =
    {
        TabPreset.FreeCompany,
        TabPreset.Linkshells,
        TabPreset.Local,
    };

    private readonly DropdownMenu rowMenu = new();
    private readonly DropdownMenu.Item[] rowMenuItems = new DropdownMenu.Item[4];
    private readonly byte[] rowMenuActions = new byte[4];
    private string rowMenuKey = string.Empty;
    private string threadKey = string.Empty;

    private void DrawChatsTab(Rect content)
    {
        inbox.Sync();
        var scale = UiScale.Current;
        UiAnchors.Report("messages.list", content);
        if (inbox.Rows.Count == 0 && inbox.Pinned.Count == 0)
        {
            DrawEmptyState(content);
            return;
        }

        var railHeight = inbox.Pinned.Count > 0 ? RailHeight * scale : 0f;
        if (railHeight > 0f)
        {
            DrawPinnedRail(new Rect(content.Min, new Vector2(content.Max.X, content.Min.Y + railHeight)));
        }

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + railHeight), content.Max);
        using (AppSurface.Begin(body))
        {
            var rows = inbox.Rows;
            for (var index = 0; index < rows.Count; index++)
            {
                var action = InboxRowView.Draw(rows[index], frameTheme, lodestone);
                if (action == InboxRowAction.Open)
                {
                    OpenConversation(rows[index].Key);
                }
                else if (action == InboxRowAction.Menu)
                {
                    rowMenuKey = rows[index].Key;
                    rowMenu.Toggle("linkpearl.row.menu", new Rect(ImGui.GetMousePos(),
                        ImGui.GetMousePos() + new Vector2(1f, 1f)));
                }
            }

            DrawNewTabRow(scale);
        }
    }

    private void DrawNewTabRow(float scale)
    {
        if (!tabs.CanAdd)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + 52f * scale));
        ImGui.Dummy(new Vector2(width, 52f * scale));
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var drawList = ImGui.GetWindowDrawList();
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X + Metrics.Space.Xs * scale, row.Min.Y + 3f * scale),
                new Vector2(row.Max.X - Metrics.Space.Xs * scale, row.Max.Y - 3f * scale), Metrics.Radius.Md * scale,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.05f)));
        }

        var iconCenter = new Vector2(row.Min.X + 35f * scale, row.Center.Y);
        AppSkin.Icon(drawList, iconCenter, FontAwesomeIcon.Plus.ToIconString(), frameTheme.Accent, 0.8f);
        var label = Loc.T(L.Linkpearl.NewTab);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(iconCenter.X + 22f * scale, row.Center.Y - labelSize.Y * 0.5f), label,
            frameTheme.Accent, TextStyles.BodyEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            CreateTab();
        }
    }

    private void DrawPinnedRail(Rect rail)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var pinned = inbox.Pinned;
        var diameter = PinDiameter * scale;
        var spacing = PinSpacing * scale;
        var left = rail.Min.X + Metrics.Space.Lg * scale;
        for (var index = 0; index < pinned.Count; index++)
        {
            var row = pinned[index];
            var min = new Vector2(left + index * spacing, rail.Min.Y + Metrics.Space.Sm * scale);
            var max = min + new Vector2(diameter, diameter);
            if (max.X > rail.Max.X)
            {
                break;
            }

            var hovered = UiInteract.Hover(min, max);
            Squircle.Fill(drawList, min, max, diameter * 0.34f,
                ImGui.GetColorU32(Palette.WithAlpha(row.Tint, hovered ? 0.32f : 0.22f)));
            Typography.DrawCentered(drawList, (min + max) * 0.5f, Initials.Of(row.Title), row.Tint,
                TextStyles.Headline);
            if (row.Unread > 0)
            {
                var badgeCenter = new Vector2(max.X - 3f * scale, min.Y + 3f * scale);
                drawList.AddCircleFilled(badgeCenter, 8f * scale, ImGui.GetColorU32(frameTheme.Danger), 16);
                Typography.DrawCentered(drawList, badgeCenter, row.Unread > 9 ? "9+" : row.Unread.ToString(Loc.Culture),
                    White, TextStyles.Caption2);
            }

            var label = Typography.FitText(row.Title, diameter + 8f * scale, TextStyles.Caption2);
            Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, max.Y + 9f * scale), label,
                frameTheme.TextMuted, TextStyles.Caption2);
            if (UiInteract.Click(min, max, hovered))
            {
                OpenConversation(row.Key);
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                rowMenuKey = row.Key;
                rowMenu.Toggle("linkpearl.row.menu",
                    new Rect(ImGui.GetMousePos(), ImGui.GetMousePos() + new Vector2(1f, 1f)));
            }
        }
    }

    private void DrawEmptyState(Rect content)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var center = content.Center;
        ProgressRing.CenterIcon(drawList, new Vector2(center.X, center.Y - 74f * scale), FontAwesomeIcon.Comments,
            frameTheme.TextMuted, 30f * scale);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - 32f * scale),
            Loc.T(L.Linkpearl.EmptyTitle), frameTheme.TextStrong, TextStyles.Title3);
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y - 12f * scale), Loc.T(L.Linkpearl.EmptyHint),
            frameTheme.TextMuted, TextStyles.Callout, content.Width - 72f * scale);
        var buttonWidth = MathF.Min(200f * scale, content.Width - 96f * scale);
        for (var index = 0; index < Presets.Length; index++)
        {
            var top = center.Y + (46f + index * 44f) * scale;
            var min = new Vector2(center.X - buttonWidth * 0.5f, top);
            var max = new Vector2(center.X + buttonWidth * 0.5f, top + 36f * scale);
            var hovered = UiInteract.Hover(min, max);
            Squircle.Fill(drawList, min, max, Metrics.Radius.Card * scale,
                ImGui.GetColorU32(Palette.WithAlpha(frameTheme.Accent, hovered ? 0.28f : 0.18f)));
            Typography.DrawCentered(drawList, (min + max) * 0.5f, Loc.T(TabStore.PresetLabel(Presets[index])),
                frameTheme.Accent, TextStyles.BodyEmphasized);
            if (UiInteract.Click(min, max, hovered))
            {
                tabs.AddPreset(Presets[index]);
                inbox.Invalidate();
            }
        }
    }

    private void DrawRowMenu(Rect area)
    {
        if (!rowMenu.IsOpenFor("linkpearl.row.menu"))
        {
            return;
        }

        var row = inbox.Find(rowMenuKey);
        if (row is null)
        {
            rowMenu.Close();
            return;
        }

        var count = 0;
        rowMenuItems[count] = new DropdownMenu.Item(Loc.T(L.Linkpearl.MarkRead),
            FontAwesomeIcon.CheckDouble.ToIconString());
        rowMenuActions[count++] = RowMenuMarkRead;
        if (row.Tab is { } tab)
        {
            rowMenuItems[count] = new DropdownMenu.Item(Loc.T(tab.Pinned ? L.Linkpearl.Unpin : L.Linkpearl.Pin),
                FontAwesomeIcon.Thumbtack.ToIconString());
            rowMenuActions[count++] = RowMenuTogglePin;
            rowMenuItems[count] = new DropdownMenu.Item(Loc.T(L.Linkpearl.EditTab),
                FontAwesomeIcon.SlidersH.ToIconString());
            rowMenuActions[count++] = RowMenuEdit;
            rowMenuItems[count] = new DropdownMenu.Item(Loc.T(L.Linkpearl.DeleteTab),
                FontAwesomeIcon.TrashAlt.ToIconString(), true);
            rowMenuActions[count++] = RowMenuDelete;
        }

        var clicked = rowMenu.Draw(area, frameTheme, rowMenuItems.AsSpan(0, count));
        if (clicked < 0)
        {
            return;
        }

        switch (rowMenuActions[clicked])
        {
            case RowMenuMarkRead:
                inbox.MarkRead(row);
                inbox.FlushSeen();
                break;
            case RowMenuTogglePin when row.Tab is { } pinTab:
                tabs.TogglePin(pinTab);
                inbox.Invalidate();
                break;
            case RowMenuEdit when row.Tab is { } editTab:
                OpenTabEditor(editTab);
                break;
            case RowMenuDelete when row.Tab is { } deleteTab:
                AskDeleteTab(deleteTab);
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
                tabs.Delete(tab);
                inbox.Invalidate();
            },
        });

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

    private void DrawConversation(Rect area, string key)
    {
        inbox.Sync();
        var row = inbox.Find(key);
        if (row is null)
        {
            router.Reset();
            return;
        }

        inbox.Viewing = key;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Title(row), backToList);
        OpenThread(row);
        chatThread.Draw(ThreadBody(area), frameTheme);
        DrawChatMenu(area);
    }

    private void OpenThread(InboxRow row)
    {
        if (string.Equals(threadKey, row.Key, StringComparison.Ordinal))
        {
            return;
        }

        threadKey = row.Key;
        if (row.Tab is { } tab)
        {
            var channels = tab.Channels.ToArray();
            chatThread.Open(new GameChatTarget(row.Key, channels, channels, tab.SendChannel, string.Empty,
                tab.Density, channels.Length > 1));
            return;
        }

        var target = ChatStreams.IsTell(row.Key) ? SendTarget(row) : string.Empty;
        chatThread.Open(new GameChatTarget(row.Key, new[] { row.StreamKey }, new[] { GameChannels.TellKey },
            GameChannels.TellKey, target, ChatDensity.Bubbles, false));
    }

    private static string Title(InboxRow row) => row.Tab is { } tab ? tab.Name : row.Title;

    private static string SendTarget(InboxRow row) =>
        row.World.Length > 0 ? string.Concat(row.Title, "@", row.World) : row.Title;

    private static Rect ThreadBody(Rect area) =>
        new(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * UiScale.Current), area.Max);
}
