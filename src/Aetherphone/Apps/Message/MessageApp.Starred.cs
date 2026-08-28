using Aetherphone.Core;
using Aetherphone.Core.Message;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private void DrawStarred(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.StarredTitle), back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var starred = configuration.MessageStarredMessages;
        if (starred.Count == 0)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Star, Loc.T(L.Message.NoStarred), string.Empty);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            var card = GroupCard.Begin(ui, starred.Count, 58f);
            for (var index = starred.Count - 1; index >= 0; index--)
            {
                DrawStarredRow(card.NextRow(), starred[index], scale);
            }

            card.End();
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawStarredRow(Rect row, StarredMessage entry, float scale)
    {
        var title = string.Concat(entry.SenderName, "  ·  ", entry.ConversationTitle);
        var timeLabel = TimeText.DayLabel(entry.CreatedAtUnix);
        var timeSize = Typography.Measure(timeLabel, TextStyles.Caption1);
        Typography.Draw(new Vector2(row.Max.X - timeSize.X, row.Min.Y + 11f * scale), timeLabel,
            ui.MutedInk, TextStyles.Caption1);
        var textWidth = row.Width - timeSize.X - 10f * scale;
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 10f * scale),
            Typography.FitText(title, textWidth, 0.88f, FontWeight.SemiBold), theme.TextStrong, 0.88f,
            FontWeight.SemiBold);
        var previewLeft = row.Min.X;
        if (entry.Kind is 1 or 3 or ChatText.LocationKind or ChatText.MusterKind)
        {
            var glyph = entry.Kind switch
            {
                3 => FontAwesomeIcon.Microphone,
                ChatText.LocationKind => FontAwesomeIcon.MapMarkerAlt,
                ChatText.MusterKind => FontAwesomeIcon.Bullhorn,
                _ => FontAwesomeIcon.Camera,
            };
            AppSkin.Icon(new Vector2(previewLeft + 6f * scale, row.Min.Y + 38f * scale), IconGlyph.Of(glyph),
                ui.MutedInk, 0.62f);
            previewLeft += 16f * scale;
        }

        var unstarRadius = 12f * scale;
        var unstarCenter = new Vector2(row.Max.X - unstarRadius + 4f * scale, row.Max.Y - 16f * scale);
        Typography.Draw(new Vector2(previewLeft, row.Min.Y + 31f * scale),
            Typography.FitText(entry.Preview, unstarCenter.X - unstarRadius - 8f * scale - previewLeft, 0.82f,
                FontWeight.Regular), ui.MutedInk, 0.82f);
        var unstarHit = new Vector2(unstarRadius, unstarRadius);
        var overUnstar = UiInteract.Hover(unstarCenter - unstarHit, unstarCenter + unstarHit);
        var unstarClicked = ui.IconButton(unstarCenter, unstarRadius, IconGlyph.Of(FontAwesomeIcon.Star),
            ui.Accent, AppSkin.Transparent, 0.8f, Loc.T(L.Message.UnstarAction));
        var band = RowBand(row, scale);
        if (unstarClicked)
        {
            configuration.MessageStarredMessages.Remove(entry);
            configuration.Save();
        }
        else if (!overUnstar && UiInteract.HoverClick(band.Min, band.Max))
        {
            router.Push(MessageRoute.Thread(entry.ConversationId));
            threadView.RequestScrollTo(entry.MessageId);
        }
    }
}
