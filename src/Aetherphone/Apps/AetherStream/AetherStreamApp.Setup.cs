using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float SetupCardHeight = 74f;
    private const float SetupButtonHeight = 46f;

    private bool setupDismissed;
    private bool setupChecked;

    private bool NeedsSetup => !screen.Engine.Dependencies.IsReady && !setupDismissed;

    private void DrawSetupGate(Rect area, float scale)
    {
        var dependencies = screen.Engine.Dependencies;
        ui.Body(area);

        if (!setupChecked)
        {
            setupChecked = true;
            dependencyWork.Run("check components",
                async token => await dependencies.CheckSizesAsync(token).ConfigureAwait(false));
        }

        var margin = Metrics.Space.Xl * scale;
        var content = new Rect(new Vector2(area.Min.X + margin, area.Min.Y + margin),
            new Vector2(area.Max.X - margin, area.Max.Y - margin));
        var width = content.Width;
        var drawList = ImGui.GetWindowDrawList();

        var tileSize = 64f * scale;
        var tileMin = new Vector2(content.Center.X - tileSize * 0.5f, content.Min.Y + Metrics.Space.Xl * scale);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
            tileSize * Metrics.Radius.TileFactor, IconTile.Surface(ui.Accent));
        ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tileSize * 0.5f, tileSize * 0.5f),
            FontAwesomeIcon.Tv, AccentRing.Ink, tileSize * 0.5f);

        var titleY = tileMin.Y + tileSize + Metrics.Space.Lg * scale;
        var title = Loc.T(L.AetherStream.SetupTitle);
        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, titleY + titleHeight * 0.5f), title,
            ui.TitleInk, TextStyles.Title2);

        var bodyY = titleY + titleHeight + Metrics.Space.Sm * scale;
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(content.Center.X, bodyY),
            Loc.T(L.AetherStream.SetupBody), ui.MutedInk, TextStyles.Subheadline, width);

        var cardsTop = bodyY + bodyHeight + Metrics.Space.Xl * scale;
        var cardHeight = SetupCardHeight * scale;
        var gap = Metrics.Space.Sm * scale;

        DrawSetupCard(new Rect(new Vector2(content.Min.X, cardsTop),
                new Vector2(content.Max.X, cardsTop + cardHeight)), dependencies.VideoLibrary,
            L.AetherStream.SetupVideoEngine, L.AetherStream.SetupVideoEngineDetail, scale);

        var secondTop = cardsTop + cardHeight + gap;
        DrawSetupCard(new Rect(new Vector2(content.Min.X, secondTop),
                new Vector2(content.Max.X, secondTop + cardHeight)), dependencies.LinkResolver,
            L.AetherStream.SetupLinkResolver, L.AetherStream.SetupLinkResolverDetail, scale);

        var buttonHeight = SetupButtonHeight * scale;
        var buttonTop = content.Max.Y - buttonHeight - Typography.LineHeight(TextStyles.Subheadline)
            - Metrics.Space.Lg * scale;
        DrawSetupAction(new Rect(new Vector2(content.Min.X, buttonTop),
            new Vector2(content.Max.X, buttonTop + buttonHeight)), dependencies, scale);
    }

    private void DrawSetupCard(Rect card, MediaDependency dependency, LocString name, LocString detail, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var snapshot = dependency.Snapshot();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Card * scale);

        var pad = Metrics.Space.Md * scale;
        var markerRadius = 7f * scale;
        var markerCenter = new Vector2(card.Min.X + pad + markerRadius, card.Min.Y + pad + markerRadius);
        DrawSetupMarker(drawList, markerCenter, markerRadius, snapshot);

        var textLeft = markerCenter.X + markerRadius + Metrics.Space.Md * scale;
        var textRight = card.Max.X - pad;
        var nameHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, card.Min.Y + pad),
            Typography.FitText(Loc.T(name), textRight - textLeft, TextStyles.BodyEmphasized), ui.TitleInk,
            TextStyles.BodyEmphasized);

        var statusText = SetupStatusText(snapshot);
        var statusColor = snapshot.State == DependencyState.Failed ? theme.Danger : ui.MutedInk;
        var detailY = card.Min.Y + pad + nameHeight + 1f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, detailY),
            Typography.FitText(Loc.T(detail), textRight - textLeft, TextStyles.Caption1), ui.MutedInk,
            TextStyles.Caption1);

        var statusY = detailY + Typography.LineHeight(TextStyles.Caption1) + 3f * scale;
        Typography.Draw(drawList, new Vector2(textLeft, statusY),
            Typography.FitText(statusText, textRight - textLeft, TextStyles.Caption1), statusColor,
            TextStyles.Caption1);

        if (snapshot.State != DependencyState.Downloading)
        {
            return;
        }

        var trackY = card.Max.Y - pad - 2f * scale;
        var track = new Rect(new Vector2(textLeft, trackY - 2f * scale),
            new Vector2(textRight, trackY + 2f * scale));
        Scrubber.Draw(track, snapshot.Fraction, ui.Accent, Palette.WithAlpha(ui.MutedInk, 0.3f), 1f);
    }

    private void DrawSetupMarker(ImDrawListPtr drawList, Vector2 center, float radius, DependencyProgress snapshot)
    {
        switch (snapshot.State)
        {
            case DependencyState.Ready:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.ToggleOn), 20);
                AppSkin.Icon(drawList, center, FontAwesomeIcon.Check.ToIconString(), new Vector4(1f, 1f, 1f, 1f),
                    0.34f);
                return;
            case DependencyState.Failed:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.Danger), 20);
                return;
            case DependencyState.Downloading:
            case DependencyState.Installing:
            case DependencyState.Checking:
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.Accent), 20);
                return;
            default:
                drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.6f)), 20,
                    Metrics.Stroke.Thin);
                return;
        }
    }

    private static string SetupStatusText(DependencyProgress snapshot)
    {
        switch (snapshot.State)
        {
            case DependencyState.Ready:
                return Loc.T(L.AetherStream.SetupReady);
            case DependencyState.Checking:
                return Loc.T(L.AetherStream.SetupChecking);
            case DependencyState.Installing:
                return Loc.T(L.AetherStream.SetupInstalling);
            case DependencyState.Failed:
                return snapshot.FailureReason ?? Loc.T(L.AetherStream.SetupFailed);
            case DependencyState.Downloading:
                return snapshot.TotalBytes > 0
                    ? string.Format(Loc.T(L.AetherStream.SetupProgress), FormatMegabytes(snapshot.ReceivedBytes),
                        FormatMegabytes(snapshot.TotalBytes))
                    : Loc.T(L.AetherStream.SetupDownloading);
            default:
                return snapshot.TotalBytes > 0
                    ? string.Format(Loc.T(L.AetherStream.SetupSize), FormatMegabytes(snapshot.TotalBytes))
                    : Loc.T(L.AetherStream.SetupWaiting);
        }
    }

    private void DrawSetupAction(Rect button, MediaDependencies dependencies, float scale)
    {
        var library = dependencies.VideoLibrary.Snapshot();
        var resolver = dependencies.LinkResolver.Snapshot();
        var busy = IsDependencyBusy(library) || IsDependencyBusy(resolver);
        var failed = library.State == DependencyState.Failed || resolver.State == DependencyState.Failed;

        var pending = dependencies.PendingDownloadBytes;
        var label = busy
            ? Loc.T(L.AetherStream.SetupInstalling)
            : failed
                ? Loc.T(L.AetherStream.SetupRetry)
                : pending > 0
                    ? string.Format(Loc.T(L.AetherStream.SetupInstallSized), FormatMegabytes(pending))
                    : Loc.T(L.AetherStream.SetupInstall);

        if (AppSkin.PillButton(button, label, true, !busy, accentedTheme) && !busy)
        {
            dependencyWork.Run("install components",
                async token => await dependencies.EnsureReadyAsync(token).ConfigureAwait(false));
        }

        var linkTop = button.Max.Y + Metrics.Space.Sm * scale;
        var linkHeight = Typography.LineHeight(TextStyles.Subheadline);
        var linkRect = new Rect(new Vector2(button.Min.X, linkTop),
            new Vector2(button.Max.X, linkTop + linkHeight));
        var hovered = UiInteract.Hover(linkRect.Min, linkRect.Max);
        Typography.DrawCentered(ImGui.GetWindowDrawList(),
            new Vector2(linkRect.Center.X, linkRect.Center.Y), Loc.T(L.AetherStream.SetupNotNow),
            hovered ? ui.TitleInk : ui.MutedInk, TextStyles.Subheadline);
        if (UiInteract.Click(linkRect.Min, linkRect.Max, hovered))
        {
            setupDismissed = true;
        }
    }

    private static bool IsDependencyBusy(DependencyProgress snapshot) =>
        snapshot.State is DependencyState.Checking or DependencyState.Downloading or DependencyState.Installing;
}
