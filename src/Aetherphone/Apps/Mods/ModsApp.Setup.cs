using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Mods;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Mods;

internal sealed partial class ModsApp
{
    private const float SetupCardHeight = 64f;
    private const float SetupButtonHeight = 40f;
    private const string PluginInstallerCommand = "/xlplugins";
    private static readonly Vector4 ReadyGreen = new(0.36f, 0.80f, 0.52f, 1f);
    private static readonly Vector4 MissingRed = new(0.94f, 0.36f, 0.42f, 1f);

    private void DrawSetup(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Mods.SetupTitle), back);
        var margin = Metrics.Space.Xl * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, top + Metrics.Space.Md * scale),
            new Vector2(area.Max.X - margin, area.Max.Y - margin));
        var drawList = ImGui.GetWindowDrawList();

        var tileSize = 64f * scale;
        var tileMin = new Vector2(content.Center.X - tileSize * 0.5f, content.Min.Y);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
            tileSize * Metrics.Radius.TileFactor, IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tileSize * 0.5f, tileSize * 0.5f),
            FontAwesomeIcon.Cube, AccentRing.Ink, tileSize * 0.5f);

        var bodyY = tileMin.Y + tileSize + Metrics.Space.Md * scale;
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(content.Center.X, bodyY),
            Loc.T(L.Mods.SetupBody), ui.MutedInk, TextStyles.Subheadline, content.Width);

        var cardsTop = bodyY + bodyHeight + Metrics.Space.Lg * scale;
        var cardHeight = SetupCardHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var penumbraReady = PenumbraBridge.IsAvailable();
        var heliosphereReady = PluginLoaded();
        DrawSetupCard(new Rect(new Vector2(content.Min.X, cardsTop), new Vector2(content.Max.X, cardsTop + cardHeight)),
            FontAwesomeIcon.LayerGroup, Loc.T(L.Mods.PenumbraSection), Loc.T(L.Mods.SetupPenumbraDetail),
            penumbraReady, scale);
        var secondTop = cardsTop + cardHeight + gap;
        DrawSetupCard(new Rect(new Vector2(content.Min.X, secondTop), new Vector2(content.Max.X, secondTop + cardHeight)),
            FontAwesomeIcon.Sun, Loc.T(L.Mods.HeliosphereSection), Loc.T(L.Mods.SetupHeliosphereDetail),
            heliosphereReady, scale);

        var hintTop = secondTop + cardHeight + Metrics.Space.Md * scale;
        var hintHeight = Typography.DrawWrappedCentered(new Vector2(content.Center.X, hintTop),
            Loc.T(L.Mods.CopyRepositoryHint), ui.MutedInk, TextStyles.Footnote, content.Width);

        var buttonHeight = SetupButtonHeight * scale;
        var copyTop = MathF.Max(hintTop + hintHeight + Metrics.Space.Lg * scale,
            content.Max.Y - buttonHeight * 2f - gap);
        var copyRect = new Rect(new Vector2(content.Min.X, copyTop), new Vector2(content.Max.X, copyTop + buttonHeight));
        if (ui.PillButton(copyRect, Loc.T(L.Mods.CopyRepository), true, "mods.copyrepo"))
        {
            ImGui.SetClipboardText(ModsContent.RepositoryUrl);
            ShellToast.Show();
        }

        var installerTop = copyRect.Max.Y + gap;
        var installerRect = new Rect(new Vector2(content.Min.X, installerTop),
            new Vector2(content.Max.X, installerTop + buttonHeight));
        if (ui.GhostButton(installerRect, Loc.T(L.Mods.OpenInstaller)))
        {
            Plugin.CommandManager.ProcessCommand(PluginInstallerCommand);
        }
    }

    private void DrawSetupCard(Rect card, FontAwesomeIcon icon, string name, string detail, bool ready, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = Metrics.Space.Md * scale;
        var tileSide = Metrics.Size.IconTile * scale * 1.3f;
        var tileCenter = new Vector2(card.Min.X + pad + tileSide * 0.5f, card.Center.Y);
        IconTile.Draw(tileCenter, tileSide, IconTile.Surface(ui.Accent), icon);

        var status = Loc.T(ready ? L.Mods.Ready : L.Mods.StatusMissing);
        var statusColor = ready ? ReadyGreen : MissingRed;
        var statusWidth = Typography.Measure(status, TextStyles.FootnoteEmphasized).X;
        var right = card.Max.X - pad;
        var dotRadius = 4f * scale;
        var statusLeft = right - statusWidth;
        Typography.Draw(drawList, new Vector2(statusLeft, card.Center.Y - Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f),
            status, statusColor, TextStyles.FootnoteEmphasized);
        drawList.AddCircleFilled(new Vector2(statusLeft - dotRadius - 6f * scale, card.Center.Y), dotRadius,
            ImGui.GetColorU32(statusColor), 16);

        var textLeft = tileCenter.X + tileSide * 0.5f + pad;
        var textWidth = MathF.Max(1f, statusLeft - dotRadius * 2f - 12f * scale - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var detailHeight = Typography.LineHeight(TextStyles.Footnote);
        var textTop = card.Center.Y - (nameHeight + detailHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop), Typography.FitText(name, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight),
            Typography.FitText(detail, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
    }
}
