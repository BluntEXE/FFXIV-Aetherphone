using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomApp : IMiniGame
{
    private const string GameId = "doom";
    private const float ScreenAspect = 4f / 3f;
    private const float TipToastSeconds = 6f;
    private const float TipCaptionInset = 14f;
    private const float CardHeight = 66f;
    private static readonly Vector4 TheaterBackdrop = new(0f, 0f, 0f, 1f);
    private readonly DoomAssets assets = new();
    private DoomRuntime? runtime;
    private string? failure;
    private bool dragging;
    private float lastDragX;
    private float tipProgress = 1f;
    public string Id => GameId;
    public Vector4 Accent => AppAccents.For(Id);
    public string Title => Loc.T(L.Games.Doom);
    public string Genre => Loc.T(L.Games.GenreArcade);
    public bool RunsOnAClock => true;
    public bool WantsLandscape => true;

    public void Open()
    {
        assets.RefreshStates();
        failure = null;
    }

    public void Close()
    {
        DisposeRuntime();
    }

    public void Dispose()
    {
        DisposeRuntime();
        assets.Dispose();
    }

    private void DisposeRuntime()
    {
        runtime?.Dispose();
        runtime = null;
        dragging = false;
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        var body = context.Body;
        var iwad = assets.IwadPath();
        if (iwad is null)
        {
            GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
            DrawSetup(body, theme, scale);
            return;
        }

        if (failure is not null)
        {
            GameScene.Ambient(ImGui.GetWindowDrawList(), body, Accent);
            DrawFailure(body, theme, scale);
            return;
        }

        if (runtime is null || runtime.Finished)
        {
            DisposeRuntime();
            if (!TryStart(iwad))
            {
                return;
            }
        }

        DrawGame(context, body, theme, scale);
    }

    private bool TryStart(string iwad)
    {
        try
        {
            runtime = new DoomRuntime(iwad, assets.SoundfontPath(), assets.Folder);
            tipProgress = 0f;
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "[Doom] The engine could not start.");
            failure = exception.Message;
            runtime = null;
            return false;
        }
    }

    private static Rect FitScreen(Rect body)
    {
        var height = body.Height;
        var width = height * ScreenAspect;
        if (width > body.Width)
        {
            width = body.Width;
            height = width / ScreenAspect;
        }

        var min = new Vector2(body.Center.X - width * 0.5f, body.Center.Y - height * 0.5f);
        return new Rect(min, min + new Vector2(width, height));
    }

    private void DrawGame(in GameContext context, Rect body, PhoneTheme theme, float scale)
    {
        var active = runtime!;
        var screen = FitScreen(body);
        var running = context.DeltaSeconds > 0f;
        active.Muted = !running;
        var keyboard = running && GameInput.Claim();
        ReadKeyboard(active.Input, keyboard);
        ReadDrag(active.Input, screen);
        try
        {
            active.Tick(context.DeltaSeconds, keyboard);
            active.Render();
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "[Doom] The engine stopped.");
            failure = exception.Message;
            DisposeRuntime();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(TheaterBackdrop), theme.ScreenRounding * scale);
        active.Present(drawList, screen);
        tipProgress = GameBanner.Advance(tipProgress, context.DeltaSeconds, TipToastSeconds);
        if (tipProgress < 1f)
        {
            GameBanner.Draw(drawList, new Vector2(screen.Center.X, screen.Max.Y - TipCaptionInset * scale * 3f),
                Loc.T(L.Games.DoomControls), Accent, theme, tipProgress, TextStyles.Subheadline);
        }
    }

    private static void ReadKeyboard(DoomInput input, bool keyboard)
    {
        input.SetHeld(DoomAction.Forward, keyboard && (ImGui.IsKeyDown(ImGuiKey.W) || ImGui.IsKeyDown(ImGuiKey.UpArrow)));
        input.SetHeld(DoomAction.Backward, keyboard && (ImGui.IsKeyDown(ImGuiKey.S) || ImGui.IsKeyDown(ImGuiKey.DownArrow)));
        input.SetHeld(DoomAction.StrafeLeft, keyboard && ImGui.IsKeyDown(ImGuiKey.A));
        input.SetHeld(DoomAction.StrafeRight, keyboard && ImGui.IsKeyDown(ImGuiKey.D));
        input.SetHeld(DoomAction.TurnLeft, keyboard && ImGui.IsKeyDown(ImGuiKey.LeftArrow));
        input.SetHeld(DoomAction.TurnRight, keyboard && ImGui.IsKeyDown(ImGuiKey.RightArrow));
        input.SetHeld(DoomAction.Fire, keyboard && (ImGui.IsKeyDown(ImGuiKey.Space) || ImGui.IsKeyDown(ImGuiKey.LeftCtrl)));
        input.SetHeld(DoomAction.Use, keyboard && (ImGui.IsKeyDown(ImGuiKey.E) || ImGui.IsKeyDown(ImGuiKey.LeftShift)));
        for (var weapon = 0; weapon < 7; weapon++)
        {
            input.SetWeapon(weapon, keyboard && ImGui.IsKeyDown(ImGuiKey.Key1 + weapon));
        }
    }

    private void ReadDrag(DoomInput input, Rect screen)
    {
        var mouse = ImGui.GetMousePos();
        if (!dragging)
        {
            if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !UiInteract.Hover(screen.Min, screen.Max))
            {
                return;
            }

            dragging = true;
            lastDragX = mouse.X;
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
            return;
        }

        input.AddTurn(mouse.X - lastDragX);
        lastDragX = mouse.X;
    }

    private void DrawSetup(Rect body, PhoneTheme theme, float scale)
    {
        assets.RefreshStates();
        var drawList = ImGui.GetWindowDrawList();
        var landscape = body.IsLandscape();
        var margin = 18f * scale;
        var content = new Rect(body.Min + new Vector2(margin, margin), body.Max - new Vector2(margin, margin));
        var cursorY = content.Min.Y;
        if (!landscape)
        {
            var tileSize = 64f * scale;
            var tileMin = new Vector2(content.Center.X - tileSize * 0.5f, cursorY + 12f * scale);
            IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tileSize, tileSize),
                tileSize * Metrics.Radius.TileFactor, IconTile.Surface(Accent));
            ProgressRing.CenterIcon(drawList, tileMin + new Vector2(tileSize * 0.5f, tileSize * 0.5f),
                FontAwesomeIcon.Skull, AccentRing.Ink, tileSize * 0.5f);
            cursorY = tileMin.Y + tileSize + 16f * scale;
        }

        var titleHeight = Typography.LineHeight(TextStyles.Title2);
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, cursorY + titleHeight * 0.5f),
            Loc.T(L.Games.DoomSetupTitle), theme.TextStrong, TextStyles.Title2);
        var bodyY = cursorY + titleHeight + 8f * scale;
        var bodyHeight = Typography.DrawWrappedCentered(new Vector2(content.Center.X, bodyY), Loc.T(L.Games.DoomSetupBody),
            theme.TextMuted, TextStyles.Subheadline, content.Width);
        var cardTop = bodyY + bodyHeight + 14f * scale;
        var cardHeight = CardHeight * scale;
        var gap = 8f * scale;
        Rect wadCard;
        Rect musicCard;
        if (landscape)
        {
            var cardWidth = (content.Width - gap) * 0.5f;
            wadCard = new Rect(new Vector2(content.Min.X, cardTop), new Vector2(content.Min.X + cardWidth, cardTop + cardHeight));
            musicCard = new Rect(new Vector2(content.Max.X - cardWidth, cardTop), new Vector2(content.Max.X, cardTop + cardHeight));
        }
        else
        {
            wadCard = new Rect(new Vector2(content.Min.X, cardTop), new Vector2(content.Max.X, cardTop + cardHeight));
            var secondTop = cardTop + cardHeight + gap;
            musicCard = new Rect(new Vector2(content.Min.X, secondTop), new Vector2(content.Max.X, secondTop + cardHeight));
        }

        DrawCard(wadCard, Loc.T(L.Games.DoomGameData), Loc.T(L.Games.DoomGameDataDetail), assets.Wad.Snapshot(), theme, scale);
        DrawCard(musicCard, Loc.T(L.Games.DoomMusic), Loc.T(L.Games.DoomMusicDetail), assets.Soundfont.Snapshot(), theme, scale);
        var busy = assets.Installing;
        var wadState = assets.Wad.Snapshot().State;
        var pending = assets.PendingBytes();
        var label = busy
            ? Loc.T(L.AetherStream.SetupInstalling)
            : wadState == DependencyState.Failed
                ? Loc.T(L.AetherStream.SetupRetry)
                : string.Format(Loc.T(L.AetherStream.SetupInstallSized), DependencySetup.FormatMegabytes(pending));
        var buttonCenter = new Vector2(content.Center.X, MathF.Max(wadCard.Max.Y, musicCard.Max.Y) + 34f * scale);
        var buttonWidth = landscape ? content.Width * 0.4f : content.Width * 0.7f;
        if (GameHud.Button(buttonCenter, new Vector2(buttonWidth, 44f * scale), label, Accent, theme) && !busy)
        {
            assets.Install();
        }
    }

    private void DrawCard(Rect card, string title, string detail, DependencyProgress snapshot, PhoneTheme theme,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = 14f * scale;
        Material.Frosted(drawList, card.Min, card.Max, radius, scale);
        var pad = 12f * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(left, card.Min.Y + pad * 0.7f),
            Typography.FitText(title, right - left, TextStyles.BodyEmphasized), theme.TextStrong, TextStyles.BodyEmphasized);
        var detailY = card.Min.Y + pad * 0.7f + titleHeight;
        Typography.Draw(drawList, new Vector2(left, detailY), Typography.FitText(detail, right - left, TextStyles.Caption1),
            theme.TextMuted, TextStyles.Caption1);
        var statusY = detailY + Typography.LineHeight(TextStyles.Caption1) + 2f * scale;
        var statusColor = snapshot.State == DependencyState.Failed
            ? theme.Danger
            : snapshot.State == DependencyState.Ready ? Accent : theme.TextMuted;
        Typography.Draw(drawList, new Vector2(left, statusY),
            Typography.FitText(DependencySetup.StatusText(snapshot), right - left, TextStyles.Caption1), statusColor,
            TextStyles.Caption1);
        if (snapshot.State != DependencyState.Downloading)
        {
            return;
        }

        var barTop = card.Max.Y - 5f * scale;
        var barMin = new Vector2(left, barTop);
        var barMax = new Vector2(right, barTop + 3f * scale);
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), 1.5f * scale);
        drawList.AddRectFilled(barMin, new Vector2(left + (right - left) * snapshot.Fraction, barMax.Y),
            ImGui.GetColorU32(Accent), 1.5f * scale);
    }

    private void DrawFailure(Rect body, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var center = body.Center;
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - 40f * scale), Loc.T(L.Games.DoomFailed),
            theme.TextStrong, TextStyles.Title3);
        Typography.DrawWrappedCentered(new Vector2(center.X, center.Y - 14f * scale), failure ?? string.Empty,
            theme.TextMuted, TextStyles.Caption1, body.Width - 40f * scale);
        if (GameHud.Button(new Vector2(center.X, center.Y + 60f * scale), new Vector2(MathF.Min(body.Width * 0.5f, 260f * scale), 44f * scale),
                Loc.T(L.AetherStream.SetupRetry), Accent, theme))
        {
            failure = null;
        }
    }
}
