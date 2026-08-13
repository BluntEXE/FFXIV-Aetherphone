using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct ChatLineStyle
{
    public readonly Vector4 Rail;
    public readonly bool ShowSender;
    public readonly bool Ghost;
    public readonly float Entrance;

    public ChatLineStyle(Vector4 rail, bool showSender, bool ghost = false, float entrance = 1f)
    {
        Rail = rail;
        ShowSender = showSender;
        Ghost = ghost;
        Entrance = entrance;
    }
}

internal static class ChatLineView
{
    private const float RailWidth = 2f;
    private const float TextInset = 12f;
    private const float LineGap = 4f;
    private const float MentionTint = 0.10f;
    private const float GhostAlpha = 0.55f;

    public static bool Draw(ChatEntry entry, PhoneTheme theme, in ChatLineStyle style, Vector4 accent)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var available = ScrollLayout.StableContentWidth();
        var textLeft = origin.X + TextInset * scale;
        var wrap = MathF.Max(24f * scale, available - TextInset * scale - 4f * scale);
        var alpha = Math.Clamp(style.Entrance, 0f, 1f) * (style.Ghost ? GhostAlpha : 1f);
        var senderHeight = style.ShowSender ? Typography.LineHeight(TextStyles.FootnoteEmphasized) : 0f;
        var bodyHeight = Typography.MeasureWrappedBlock(entry.Text, TextStyles.Callout, wrap).Y;
        var totalHeight = senderHeight + bodyHeight;
        var lineMin = new Vector2(origin.X, origin.Y);
        var lineMax = new Vector2(origin.X + available, origin.Y + totalHeight);
        if (entry.IsMention)
        {
            Squircle.Fill(drawList, lineMin, new Vector2(lineMax.X, lineMax.Y + LineGap * scale * 0.5f),
                Metrics.Radius.Sm * scale, ImGui.GetColorU32(Palette.WithAlpha(accent, MentionTint * alpha)));
        }

        var railColor = entry.IsMention ? accent : style.Rail;
        if (railColor.W > 0f)
        {
            var railLeft = origin.X + 2f * scale;
            drawList.AddRectFilled(new Vector2(railLeft, origin.Y + 2f * scale),
                new Vector2(railLeft + RailWidth * scale, lineMax.Y),
                ImGui.GetColorU32(Palette.WithAlpha(railColor, railColor.W * alpha)), RailWidth * scale * 0.5f);
        }

        if (style.ShowSender)
        {
            var tint = entry.IsSelf ? accent : SenderTint.Of(entry.AuthorName);
            var name = FirstName(entry.AuthorName);
            var nameWidth = MathF.Min(wrap * 0.62f, Typography.Measure(name, TextStyles.FootnoteEmphasized).X);
            Typography.Draw(drawList, new Vector2(textLeft, origin.Y),
                Typography.FitText(name, nameWidth, TextStyles.FootnoteEmphasized),
                Palette.WithAlpha(tint, tint.W * alpha), TextStyles.FootnoteEmphasized);
            var stamp = TimeText.Clock(entry.At);
            Typography.Draw(drawList, new Vector2(textLeft + nameWidth + 6f * scale, origin.Y + 1f * scale), stamp,
                Palette.WithAlpha(theme.TextMuted, theme.TextMuted.W * alpha), TextStyles.Caption2);
        }

        var bodyTop = new Vector2(textLeft, origin.Y + senderHeight);
        var ink = Palette.WithAlpha(theme.TextStrong, theme.TextStrong.W * alpha);
        using (Plugin.Fonts.Push(TextStyles.Callout.Scale, TextStyles.Callout.Weight))
        {
            var layout = LinkText.LayoutFor(entry.Text, wrap);
            if (layout is null)
            {
                Plugin.Fonts.NoticeText(entry.Text);
                var lines = Typography.WrapCurrent(entry.Text, wrap);
                var font = ImGui.GetFont();
                var fontSize = ImGui.GetFontSize();
                var lineHeight = ImGui.GetTextLineHeightWithSpacing();
                var packed = ImGui.GetColorU32(ink);
                for (var index = 0; index < lines.Length; index++)
                {
                    drawList.AddText(font, fontSize, new Vector2(bodyTop.X, bodyTop.Y + index * lineHeight), packed,
                        lines[index]);
                }
            }
            else
            {
                LinkText.Draw(drawList, layout, bodyTop, 1f, ink, Palette.WithAlpha(accent, accent.W * alpha), alpha,
                    !style.Ghost && style.Entrance >= 1f);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, lineMax.Y + LineGap * scale));
        return !style.Ghost && style.Entrance >= 1f && UiInteract.Hover(lineMin, lineMax) &&
               ImGui.IsMouseClicked(ImGuiMouseButton.Right);
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
