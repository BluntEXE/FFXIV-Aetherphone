using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private string forwardFilter = string.Empty;
    private bool forwardBusy;
    private string? forwardOpenPending;

    private void DrawForwardPicker(Rect area, string messageId)
    {
        var message = store.FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            var context = new PhoneContext(area, theme, navigation);
            AppHeader.Draw(context, Loc.T(L.Message.ForwardTitle), back);
            return;
        }

        if (DrawConversationPicker(area, Loc.T(L.Message.ForwardTitle), ref forwardFilter) is not { } target ||
            forwardBusy)
        {
            return;
        }

        forwardBusy = true;
        store.ForwardMessage(message, target.Id, _ => forwardBusy = false);
        forwardOpenPending = target.Id;
    }

    private ConversationDto? DrawConversationPicker(Rect area, string title, ref string filter)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, title, back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var searchHeight = 52f * scale;
        SearchField.DrawSubmit(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
            "##conversationPickerFilter", Loc.T(L.Phone.FilterHint), ref filter, AppPalettes.Message);
        var listRect = new Rect(new Vector2(area.Min.X, top + searchHeight), area.Max);
        var snapshot = store.Conversations;
        var query = filter.Trim();
        ConversationDto? picked = null;
        using (AppSurface.Begin(listRect))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var shown = 0;
            for (var index = 0; index < snapshot.Length; index++)
            {
                if (PickerMatches(snapshot[index], query))
                {
                    shown++;
                }
            }

            if (shown == 0)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    Loc.T(L.Phone.NoOneFound), ui.MutedInk);
            }
            else
            {
                var card = GroupCard.Begin(ui, shown, 56f);
                for (var index = 0; index < snapshot.Length; index++)
                {
                    var item = snapshot[index];
                    if (!PickerMatches(item, query))
                    {
                        continue;
                    }

                    if (DrawPickerRow(card.NextRow(), item, scale))
                    {
                        picked = item;
                    }
                }

                card.End();
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }

        return picked;
    }

    private static bool PickerMatches(ConversationDto item, string query) =>
        query.Length == 0 ||
        DirectMessagesStore.DisplayTitle(item).Contains(query, StringComparison.OrdinalIgnoreCase);

    private bool DrawPickerRow(Rect row, ConversationDto item, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 19f * scale;
        var avatarCenter = new Vector2(row.Min.X + radius, row.Center.Y);
        var title = DirectMessagesStore.DisplayTitle(item);
        if (item.IsGroup)
        {
            drawList.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)), 32);
            AppSkin.Icon(avatarCenter, FontAwesomeIcon.Users.ToIconString(), White, 0.9f);
        }
        else
        {
            AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, title, string.Empty, item.OtherAvatarUrl,
                images, lodestone, 0.9f, 32, 1f, Frames.Of(item.FrameId));
        }

        var textLeft = avatarCenter.X + radius + 12f * scale;
        var iconCenterX = row.Max.X - 8f * scale;
        var textMaxWidth = MathF.Max(1f, iconCenterX - 12f * scale - textLeft);
        var titleTop = row.Center.Y - 9f * scale;
        var titleSize = Typography.Measure(title, 1f, FontWeight.SemiBold);
        var titleHovering = UiInteract.Hover(new Vector2(textLeft, titleTop),
            new Vector2(textLeft + textMaxWidth, titleTop + titleSize.Y));
        Marquee.DrawLeft("picker.row." + item.Id, title, textLeft, titleTop, textMaxWidth,
            new TextStyle(1f, FontWeight.SemiBold), theme.TextStrong, titleHovering);
        AppSkin.Icon(new Vector2(iconCenterX, row.Center.Y),
            FontAwesomeIcon.Share.ToIconString(), ui.MutedInk, 0.85f);
        var band = RowBand(row, scale);
        return UiInteract.HoverClick(band.Min, band.Max);
    }
}
