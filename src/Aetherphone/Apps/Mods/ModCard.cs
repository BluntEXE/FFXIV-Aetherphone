using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Mods;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Mods;

internal enum InstalledCardResult : byte
{
    None,
    Open,
    Toggled,
    Update,
}

internal static class ModCard
{
    public const float Gap = 10f;
    public const float Height = 104f;
    public const float ThumbSide = 80f;
    private const float Pad = 12f;
    private const float AuthorOffset = 22f;
    private const float TaglineOffset = 40f;
    private const float PillHeight = 22f;
    private const float PillPad = 9f;
    private const float UpdateButtonWidth = 84f;
    private const float UpdateButtonHeight = 30f;

    public static readonly Vector4 AdultRed = new(0.94f, 0.36f, 0.42f, 1f);
    public static readonly Vector4 UpdateGold = new(0.96f, 0.72f, 0.28f, 1f);
    public static readonly Vector4 InstalledGreen = new(0.36f, 0.80f, 0.52f, 1f);

    public static bool Draw(Rect card, ModCardModel model, IDalamudTextureWrap? thumbnail, AppSkin ui, bool veil,
        string status, Vector4 statusColor)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        var palette = ui.Palette;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = Pad * scale;
        var thumb = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Min.X + pad + ThumbSide * scale, card.Min.Y + pad + ThumbSide * scale));
        DrawThumbnail(drawList, thumb, thumbnail, palette, veil, scale);

        var textLeft = thumb.Max.X + pad;
        var right = card.Max.X - pad;
        var titleRight = right;
        if (status.Length > 0)
        {
            titleRight = DrawStatusPill(drawList, status, statusColor, right, thumb.Min.Y, scale) - 8f * scale;
        }

        Marquee.DrawLeftAuto(drawList, model.MarqueeId, model.Name, textLeft, thumb.Min.Y,
            MathF.Max(1f, titleRight - textLeft), TextStyles.Headline, palette.TitleInk);
        var width = MathF.Max(1f, right - textLeft);
        if (model.ByLine.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, thumb.Min.Y + AuthorOffset * scale),
                Typography.FitText(model.ByLine, width, TextStyles.Footnote), palette.MutedInk, TextStyles.Footnote);
        }

        if (model.Tagline.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, thumb.Min.Y + TaglineOffset * scale),
                Typography.FitText(model.Tagline, width, TextStyles.Footnote), palette.BodyInk, TextStyles.Footnote);
        }

        DrawMetaRow(drawList, model, textLeft, right, thumb.Max.Y, palette, scale);
        return CardClick(drawList, card, rounding);
    }

    public static InstalledCardResult DrawInstalled(Rect card, InstalledMod mod, IDalamudTextureWrap? cover,
        AppSkin ui, PhoneTheme theme, bool updateRow)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        var palette = ui.Palette;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = Pad * scale;
        var thumb = new Rect(new Vector2(card.Min.X + pad, card.Min.Y + pad),
            new Vector2(card.Min.X + pad + ThumbSide * scale, card.Min.Y + pad + ThumbSide * scale));
        DrawThumbnail(drawList, thumb, cover, palette, false, scale);

        var textLeft = thumb.Max.X + pad;
        var right = card.Max.X - pad;
        Rect control;
        if (updateRow)
        {
            var buttonWidth = UpdateButtonWidth * scale;
            var buttonHeight = UpdateButtonHeight * scale;
            control = new Rect(new Vector2(right - buttonWidth, card.Center.Y - buttonHeight * 0.5f),
                new Vector2(right, card.Center.Y + buttonHeight * 0.5f));
        }
        else
        {
            var toggleWidth = Metrics.Size.ToggleWidth * scale;
            var toggleHeight = Metrics.Size.ToggleHeight * scale;
            control = new Rect(new Vector2(right - toggleWidth, card.Center.Y - toggleHeight * 0.5f),
                new Vector2(right, card.Center.Y + toggleHeight * 0.5f));
        }

        var textRight = control.Min.X - 10f * scale;
        var width = MathF.Max(1f, textRight - textLeft);
        Marquee.DrawLeftAuto(drawList, mod.TitleId, mod.Name, textLeft, thumb.Min.Y, width, TextStyles.Headline,
            palette.TitleInk);
        var subtitle = updateRow ? mod.UpdateLine : mod.Subtitle;
        Typography.Draw(drawList, new Vector2(textLeft, thumb.Min.Y + AuthorOffset * scale),
            Typography.FitText(subtitle, width, TextStyles.Footnote), palette.MutedInk, TextStyles.Footnote);
        if (mod.ByLine.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, thumb.Min.Y + TaglineOffset * scale),
                Typography.FitText(mod.ByLine, width, TextStyles.Footnote), palette.BodyInk, TextStyles.Footnote);
        }

        var result = InstalledCardResult.None;
        if (updateRow)
        {
            if (ui.PillButton(control, Loc.T(L.Mods.BadgeUpdate), true, mod.UpdateId))
            {
                result = InstalledCardResult.Update;
            }
        }
        else if (mod.Enabled is { } enabled)
        {
            var next = Toggle.Draw(mod.ToggleId, control, enabled, theme);
            if (next != enabled)
            {
                result = InstalledCardResult.Toggled;
            }
        }

        var overControl = UiInteract.Hover(control.Min, control.Max);
        if (result == InstalledCardResult.None && !overControl && CardClick(drawList, card, rounding))
        {
            result = InstalledCardResult.Open;
        }

        return result;
    }

    public static void DrawThumbnail(ImDrawListPtr drawList, Rect rect, IDalamudTextureWrap? texture,
        in AppPalette palette, bool veil, float scale)
    {
        var rounding = Metrics.Radius.Md * scale;
        if (texture is null)
        {
            Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(palette.Accent, 0.22f)),
                ImGui.GetColorU32(Palette.WithAlpha(palette.Accent, 0.08f)));
            AppSkin.Icon(drawList, rect.Center, FontAwesomeIcon.Cube.ToIconString(),
                Palette.WithAlpha(palette.Accent, 0.75f), 1.2f);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
        drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersAll);
        if (veil)
        {
            SensitiveVeil.Draw(drawList, rect.Min, rect.Max, rounding);
        }
    }

    private static float DrawStatusPill(ImDrawListPtr drawList, string status, Vector4 color, float right,
        float top, float scale)
    {
        var size = Typography.Measure(status, TextStyles.Caption2);
        var height = PillHeight * scale;
        var width = size.X + PillPad * 2f * scale;
        var min = new Vector2(right - width, top - 2f * scale);
        var max = new Vector2(right, min.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(color, 0.18f)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, status, color, TextStyles.Caption2);
        return min.X;
    }

    private static void DrawMetaRow(ImDrawListPtr drawList, ModCardModel model, float left, float right,
        float bottom, in AppPalette palette, float scale)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Caption1);
        var top = bottom - lineHeight;
        var centerY = top + lineHeight * 0.5f;
        var cursor = left;
        var iconSize = 0.62f;
        AppSkin.Icon(drawList, new Vector2(cursor + 6f * scale, centerY), FontAwesomeIcon.Download.ToIconString(),
            palette.MutedInk, iconSize);
        cursor += 16f * scale;
        Typography.Draw(drawList, new Vector2(cursor, top), model.DownloadsText, palette.MutedInk,
            TextStyles.Caption1);
        cursor += Typography.Measure(model.DownloadsText, TextStyles.Caption1).X + 12f * scale;

        if (model.Sensitive)
        {
            var badge = Loc.T(model.Nsfl ? L.Mods.BadgeNsfl : L.Mods.BadgeNsfw);
            var badgeSize = Typography.Measure(badge, TextStyles.Caption2);
            var badgeWidth = badgeSize.X + 10f * scale;
            var badgeMin = new Vector2(cursor, centerY - lineHeight * 0.5f);
            var badgeMax = new Vector2(cursor + badgeWidth, centerY + lineHeight * 0.5f);
            Squircle.Fill(drawList, badgeMin, badgeMax, lineHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(AdultRed, 0.22f)));
            Typography.DrawCentered(drawList, (badgeMin + badgeMax) * 0.5f, badge, AdultRed, TextStyles.Caption2);
            cursor += badgeWidth + 8f * scale;
        }

        if (model.CategoryLabel.Length > 0 && cursor < right)
        {
            Typography.Draw(drawList, new Vector2(cursor, top),
                Typography.FitText(model.CategoryLabel, right - cursor, TextStyles.Caption1), palette.MutedInk,
                TextStyles.Caption1);
        }
    }

    private static bool CardClick(ImDrawListPtr drawList, Rect card, float rounding)
    {
        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }
}
