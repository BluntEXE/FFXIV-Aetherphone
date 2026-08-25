using System.Text;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Mods;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Mods;

internal sealed partial class ModsApp
{
    private const float HeroAspect = 0.62f;
    private const float HeroMaxHeight = 240f;
    private const float ChipRowHeight = 34f;
    private const float ButtonHeight = 40f;
    private const float StatRowHeight = 44f;
    private const float CardPad = 14f;
    private const float CardGapPixels = 10f;
    private const int AffectsShown = 12;
    private static readonly Vector4 HeroInk = new(1f, 1f, 1f, 0.95f);
    private static readonly Vector4 HeroScrim = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 WarningAmber = new(0.98f, 0.70f, 0.24f, 1f);

    private Guid detailPackageId;
    private PackageDto? detailPackage;
    private int detailVariantIndex;
    private int detailImageIndex;
    private bool detailRevealed;
    private string detailAffects = string.Empty;
    private string[] detailTagLabels = Array.Empty<string>();
    private string[] detailVariantLabels = Array.Empty<string>();
    private string detailUpdatedText = string.Empty;
    private string detailDownloadsText = string.Empty;
    private string detailSizeText = string.Empty;

    private void ResetDetail(Guid packageId)
    {
        detailPackageId = packageId;
        detailPackage = null;
        detailVariantIndex = 0;
        detailImageIndex = 0;
        detailRevealed = false;
        detailAffects = string.Empty;
        detailTagLabels = Array.Empty<string>();
        detailVariantLabels = Array.Empty<string>();
    }

    private void DrawDetail(Rect area, Guid packageId)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var package = hub.Details.Get(packageId);
        AppHeader.Draw(context, string.Empty, back);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (package is null)
        {
            DrawDetailState(body, packageId, scale);
            return;
        }

        if (!ReferenceEquals(package, detailPackage))
        {
            PrepareDetail(package);
        }

        using (AppSurface.Begin(body))
        {
            DrawHero(package, scale);
            var inset = Metrics.Space.Lg * scale;
            ImGui.Indent(inset);
            var width = ImGui.GetContentRegionAvail().X - inset;
            DrawTitleBlock(package, width, scale);
            DrawTagRow(width, scale);
            DrawStats(package, width, scale);
            DrawVariantRow(package, width, scale);
            DrawActions(package, width, scale);
            DrawTextCard(package.ContentWarning, Loc.T(L.Mods.ContentWarning), width, scale, WarningAmber);
            var newest = NewestVersion(package);
            if (newest is not null && !string.IsNullOrWhiteSpace(newest.Changelog))
            {
                DrawTextCard(newest.Changelog, Loc.T(L.Mods.Changelog, newest.Version), width, scale, ui.Accent);
            }

            DrawTextCard(package.Description, Loc.T(L.Mods.About), width, scale, ui.Accent);
            DrawTextCard(package.Permissions, Loc.T(L.Mods.Permissions), width, scale, ui.Accent);
            DrawTextCard(detailAffects, Loc.T(L.Mods.Affects), width, scale, ui.Accent);
            ImGui.Unindent(inset);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xl * scale));
        }
    }

    private void DrawDetailState(Rect body, Guid packageId, float scale)
    {
        if (hub.Details.HasFailed(packageId))
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.CloudDownloadAlt, Loc.T(L.Mods.DetailFailed),
                    Loc.T(L.Mods.LoadFailedHint), Loc.T(L.Mods.Retry)))
            {
                hub.Details.Request(packageId, true);
            }

            return;
        }

        LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 110f * scale), 13f * scale, ui.Accent,
            ui.MutedInk, Loc.T(L.Mods.DetailLoading));
    }

    private void PrepareDetail(PackageDto package)
    {
        detailPackage = package;
        detailVariantIndex = Math.Clamp(detailVariantIndex, 0, Math.Max(0, package.Variants.Length - 1));
        detailImageIndex = 0;
        detailDownloadsText = ModsContent.FormatCount(package.Downloads ?? 0);
        detailUpdatedText = TimeText.Ago(package.UpdatedAt);

        var labels = new List<string>(package.Tags.Length + 3);
        if (package.Nsfw is { } restricted)
        {
            if (restricted.Nsfl)
            {
                labels.Add(Loc.T(L.Mods.BadgeNsfl));
            }
            else if (restricted.Nsfw)
            {
                labels.Add(Loc.T(L.Mods.BadgeNsfw));
            }

            if (restricted.Cw)
            {
                labels.Add(Loc.T(L.Mods.BadgeContentWarning));
            }
        }

        for (var index = 0; index < package.Tags.Length; index++)
        {
            if (package.Tags[index].Category)
            {
                labels.Add(ModsContent.TagLabel(package.Tags[index].Slug));
            }
        }

        detailTagLabels = labels.ToArray();
        detailVariantLabels = new string[package.Variants.Length];
        for (var index = 0; index < package.Variants.Length; index++)
        {
            detailVariantLabels[index] = package.Variants[index].Name;
        }

        RefreshVariantTexts(package);
    }

    private void RefreshVariantTexts(PackageDto package)
    {
        var newest = NewestVersion(package);
        detailAffects = BuildAffects(newest);
        detailSizeText = newest is null ? string.Empty : ModsContent.FormatBytes(newest.InstallSize);
    }

    private static string BuildAffects(VersionDto? version)
    {
        if (version is null || version.Affects.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var shown = Math.Min(AffectsShown, version.Affects.Length);
        for (var index = 0; index < shown; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(version.Affects[index]);
        }

        if (version.Affects.Length > shown)
        {
            builder.Append(' ').Append(Loc.T(L.Mods.AffectsMore, version.Affects.Length - shown));
        }

        return builder.ToString();
    }

    private VersionDto? NewestVersion(PackageDto package)
    {
        if (package.Variants.Length == 0)
        {
            return null;
        }

        var variant = package.Variants[Math.Clamp(detailVariantIndex, 0, package.Variants.Length - 1)];
        return variant.Versions.Length > 0 ? variant.Versions[0] : null;
    }

    private void DrawHero(PackageDto package, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = MathF.Min(width * HeroAspect, HeroMaxHeight * scale);
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var drawList = ImGui.GetWindowDrawList();
        var images = package.Images;
        if (images.Length == 0)
        {
            Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, 0f,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.26f)),
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.06f)));
            AppSkin.Icon(drawList, rect.Center, FontAwesomeIcon.Cube.ToIconString(),
                Palette.WithAlpha(ui.Accent, 0.6f), 2.4f);
        }
        else
        {
            DrawHeroImage(package, rect, images, drawList, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void DrawHeroImage(PackageDto package, Rect rect, ImageDto[] imageList, ImDrawListPtr drawList,
        float scale)
    {
        if (detailImageIndex >= imageList.Length)
        {
            detailImageIndex = 0;
        }

        var url = ModsContent.ImageUrl(imageList[detailImageIndex].Hash);
        var texture = images.Sized(url, rect.Width);
        if (texture is null)
        {
            drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(ui.FieldSurface));
            LoadingPulse.Spinner(rect.Center, 10f * scale, ui.Accent);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
        drawList.AddImage(texture.Handle, rect.Min, rect.Max, uv0, uv1);
        var sensitive = package.Nsfw is { } restricted && (restricted.Nsfw || restricted.Nsfl);
        if (sensitive && snapshot.BlurSensitive && !detailRevealed)
        {
            SensitiveVeil.Draw(drawList, rect.Min, rect.Max, 0f);
            Typography.DrawCentered(drawList, rect.Center, Loc.T(L.Mods.TapToReveal), HeroInk,
                TextStyles.SubheadlineEmphasized);
            var veiled = UiInteract.Hover(rect.Min, rect.Max);
            if (veiled)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(rect.Min, rect.Max, veiled))
            {
                detailRevealed = true;
            }

            return;
        }

        var scrimTop = new Vector2(rect.Min.X, rect.Max.Y - 56f * scale);
        drawList.AddRectFilledMultiColor(scrimTop, rect.Max, 0u, 0u, ImGui.GetColorU32(HeroScrim),
            ImGui.GetColorU32(HeroScrim));

        var expandRadius = 15f * scale;
        var expandCenter = new Vector2(rect.Max.X - expandRadius - 10f * scale,
            rect.Min.Y + expandRadius + 10f * scale);
        var expandHalf = new Vector2(expandRadius, expandRadius);
        var overExpand = UiInteract.Hover(expandCenter - expandHalf, expandCenter + expandHalf);
        drawList.AddCircleFilled(expandCenter, expandRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, overExpand ? 0.78f : 0.55f)), 28);
        AppSkin.Icon(drawList, expandCenter, FontAwesomeIcon.Expand.ToIconString(), HeroInk, 0.66f);
        if (overExpand)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(expandCenter - expandHalf, expandCenter + expandHalf, overExpand))
        {
            var viewerUrl = url;
            photoViewer.Open(this, () => images.Get(viewerUrl));
            return;
        }

        if (imageList.Length <= 1)
        {
            return;
        }

        DrawHeroDots(drawList, rect, imageList.Length, scale);
        var midX = rect.Center.X;
        var leftHovered = !overExpand && UiInteract.Hover(rect.Min, new Vector2(midX, rect.Max.Y));
        var rightHovered = !overExpand && UiInteract.Hover(new Vector2(midX, rect.Min.Y), rect.Max);
        if (leftHovered || rightHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rect.Min, new Vector2(midX, rect.Max.Y), leftHovered))
        {
            detailImageIndex = (detailImageIndex + imageList.Length - 1) % imageList.Length;
        }
        else if (UiInteract.Click(new Vector2(midX, rect.Min.Y), rect.Max, rightHovered))
        {
            detailImageIndex = (detailImageIndex + 1) % imageList.Length;
        }
    }

    private void DrawHeroDots(ImDrawListPtr drawList, Rect rect, int count, float scale)
    {
        var radius = 3f * scale;
        var spacing = 10f * scale;
        var totalWidth = (count - 1) * spacing;
        var startX = rect.Center.X - totalWidth * 0.5f;
        var y = rect.Max.Y - 12f * scale;
        for (var index = 0; index < count; index++)
        {
            var active = index == detailImageIndex;
            drawList.AddCircleFilled(new Vector2(startX + index * spacing, y), active ? radius : radius * 0.7f,
                ImGui.GetColorU32(active ? HeroInk : Palette.WithAlpha(HeroInk, 0.45f)), 16);
        }
    }

    private void DrawTitleBlock(PackageDto package, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var titleHeight = Typography.DrawWrappedLeft(origin, package.Name, ui.TitleInk, TextStyles.Title2, width);
        var cursorY = origin.Y + titleHeight + 2f * scale;
        if (package.User is { } user && user.VisibleName.Length > 0)
        {
            Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X, cursorY),
                Typography.FitText(Loc.T(L.Mods.By, user.VisibleName), width, TextStyles.Subheadline), ui.MutedInk,
                TextStyles.Subheadline);
            cursorY += Typography.LineHeight(TextStyles.Subheadline) + 4f * scale;
        }

        if (package.Tagline.Length > 0)
        {
            cursorY += Typography.DrawWrappedLeft(new Vector2(origin.X, cursorY), package.Tagline, ui.BodyInk,
                TextStyles.Body, width);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cursorY - origin.Y + Metrics.Space.Sm * scale));
    }

    private void DrawTagRow(float width, float scale)
    {
        if (detailTagLabels.Length == 0)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = ChipRowHeight * scale;
        var cursorX = origin.X;
        var centerY = origin.Y + rowHeight * 0.5f;
        var right = origin.X + width;
        for (var index = 0; index < detailTagLabels.Length; index++)
        {
            var label = detailTagLabels[index];
            var chipWidth = AppSkin.PillWidthFor(label, rowHeight * 0.8f);
            if (cursorX + chipWidth > right && cursorX > origin.X)
            {
                break;
            }

            ui.FlowChip(ref cursorX, centerY, 6f * scale, label, false);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Xs * scale));
    }

    private void DrawStats(PackageDto package, float width, float scale)
    {
        var newest = NewestVersion(package);
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = StatRowHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Md * scale;
        ui.Card(drawList, origin, new Vector2(origin.X + width, origin.Y + rowHeight), rounding);
        var columns = newest is null ? 2 : 3;
        var columnWidth = width / columns;
        DrawStat(drawList, origin, columnWidth, rowHeight, 0, Loc.T(L.Mods.Downloads), detailDownloadsText);
        DrawStat(drawList, origin, columnWidth, rowHeight, 1, Loc.T(L.Mods.Updated), detailUpdatedText);
        if (newest is not null)
        {
            DrawStat(drawList, origin, columnWidth, rowHeight, 2, Loc.T(L.Mods.InstallSize), detailSizeText);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Md * scale));
    }

    private void DrawStat(ImDrawListPtr drawList, Vector2 origin, float columnWidth, float rowHeight, int column,
        string label, string value)
    {
        var centerX = origin.X + columnWidth * (column + 0.5f);
        var valueHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var labelHeight = Typography.LineHeight(TextStyles.Caption1);
        var top = origin.Y + (rowHeight - valueHeight - labelHeight) * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(centerX, top + valueHeight * 0.5f),
            Typography.FitText(value, columnWidth - 8f, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.DrawCentered(drawList, new Vector2(centerX, top + valueHeight + labelHeight * 0.5f),
            Typography.FitText(label, columnWidth - 8f, TextStyles.Caption1), ui.MutedInk, TextStyles.Caption1);
    }

    private void DrawVariantRow(PackageDto package, float width, float scale)
    {
        if (package.Variants.Length <= 1)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Mods.Variants));
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = ChipRowHeight * scale;
        var cursorX = origin.X;
        var centerY = origin.Y + rowHeight * 0.5f;
        for (var index = 0; index < detailVariantLabels.Length; index++)
        {
            if (ui.FlowChip(ref cursorX, centerY, 6f * scale, detailVariantLabels[index],
                    index == detailVariantIndex) && index != detailVariantIndex)
            {
                detailVariantIndex = index;
                RefreshVariantTexts(package);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + Metrics.Space.Sm * scale));
    }

    private void DrawActions(PackageDto package, float width, float scale)
    {
        if (package.Variants.Length == 0)
        {
            return;
        }

        var variant = package.Variants[Math.Clamp(detailVariantIndex, 0, package.Variants.Length - 1)];
        var newest = variant.Versions.Length > 0 ? variant.Versions[0] : null;
        var installed = hub.Library.TryFindVariant(variant.Id, out var mod);
        var origin = ImGui.GetCursorScreenPos();
        var buttonHeight = ButtonHeight * scale;
        var primary = new Rect(origin, new Vector2(origin.X + width, origin.Y + buttonHeight));
        string label;
        var enabled = newest is not null;
        var pluginReady = PluginLoaded();
        if (!pluginReady)
        {
            label = Loc.T(L.Mods.SetUpHeliosphere);
            enabled = true;
        }
        else if (installed && newest is not null && mod.VersionId == newest.Id)
        {
            label = Loc.T(L.Mods.InstalledVersion, mod.Version);
            enabled = false;
        }
        else if (installed && newest is not null)
        {
            label = Loc.T(L.Mods.UpdateTo, newest.Version);
        }
        else
        {
            label = Loc.T(L.Mods.Install);
        }

        if (ui.PillButton(primary, label, enabled, "mods.primary") && enabled)
        {
            if (!pluginReady)
            {
                OpenSetup();
            }
            else if (newest is not null)
            {
                StartInstall(package.Id, variant.Id, newest.Id);
            }
        }

        var secondaryTop = primary.Max.Y + Metrics.Space.Sm * scale;
        var gap = Metrics.Space.Sm * scale;
        var half = (width - gap) * 0.5f;
        var siteRect = new Rect(new Vector2(origin.X, secondaryTop),
            new Vector2(origin.X + (installed ? half : width), secondaryTop + buttonHeight));
        if (ui.GhostButton(siteRect, Loc.T(L.Mods.ViewOnSite)))
        {
            UrlActions.AskThenOpen(ModsContent.ModPageUrl(package.Id));
        }

        if (installed)
        {
            var penumbraRect = new Rect(new Vector2(siteRect.Max.X + gap, secondaryTop),
                new Vector2(origin.X + width, secondaryTop + buttonHeight));
            if (ui.GhostButton(penumbraRect, Loc.T(L.Mods.OpenInPenumbra)))
            {
                hub.Library.OpenInPenumbra(mod);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonHeight * 2f + Metrics.Space.Sm * scale + Metrics.Space.Md * scale));
    }

    private void DrawTextCard(string? text, string heading, float width, float scale, Vector4 accent)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var pad = CardPad * scale;
        var innerWidth = MathF.Max(1f, width - pad * 2f);
        var headingHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var bodySize = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, innerWidth);
        var height = pad * 2f + headingHeight + 6f * scale + bodySize.Y;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var card = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (ImGui.IsRectVisible(card.Min, card.Max))
        {
            ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale);
            Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + pad), heading, accent,
                TextStyles.SubheadlineEmphasized);
            Typography.DrawWrappedLeft(new Vector2(origin.X + pad, origin.Y + pad + headingHeight + 6f * scale),
                text, ui.BodyInk, TextStyles.Footnote, innerWidth);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + CardGapPixels * scale));
    }
}
