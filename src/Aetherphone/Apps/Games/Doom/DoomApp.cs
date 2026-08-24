using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ManagedDoom;

namespace Aetherphone.Apps.Games.Doom;

internal sealed class DoomApp : IMiniGame
{
    private const string GameId = "doom";
    private const float ScreenAspect = 4f / 3f;
    private const float KeySize = 46f;
    private const float KeyGap = 8f;
    private const float Pad = 8f;
    private const float TipToastSeconds = 6f;
    private const float CardHeight = 66f;

    private readonly struct Layout
    {
        public readonly Rect Screen;
        public readonly Vector2 LeftCenter;
        public readonly Vector2 RightCenter;
        public readonly float Key;
        public readonly Vector2 TipCenter;

        public Layout(Rect screen, Vector2 leftCenter, Vector2 rightCenter, float key, Vector2 tipCenter)
        {
            Screen = screen;
            LeftCenter = leftCenter;
            RightCenter = rightCenter;
            Key = key;
            TipCenter = tipCenter;
        }
    }

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
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var iwad = assets.IwadPath();
        if (iwad is null)
        {
            DrawSetup(body, theme, scale);
            return;
        }

        if (failure is not null)
        {
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

    private static Layout Measure(Rect body, float scale)
    {
        var pad = Pad * scale;
        var gap = KeyGap * scale;
        var tipHeight = Typography.LineHeight(TextStyles.Caption1) + pad;
        if (body.IsLandscape())
        {
            var key = MathF.Min(KeySize * scale, (body.Height - pad * 2f - gap * 3f) / 4f);
            var leftWidth = key * 3f + gap * 2f;
            var rightWidth = key * 2f + gap;
            var screenHeight = body.Height - pad * 2f - tipHeight;
            var screenWidth = screenHeight * ScreenAspect;
            var available = body.Width - leftWidth - rightWidth - pad * 6f;
            if (screenWidth > available)
            {
                screenWidth = available;
                screenHeight = screenWidth / ScreenAspect;
            }

            var middleLeft = body.Min.X + pad * 2f + leftWidth;
            var middleRight = body.Max.X - pad * 2f - rightWidth;
            var screenMin = new Vector2((middleLeft + middleRight) * 0.5f - screenWidth * 0.5f,
                body.Min.Y + pad + (body.Height - pad * 2f - tipHeight - screenHeight) * 0.5f);
            var screen = new Rect(screenMin, screenMin + new Vector2(screenWidth, screenHeight));
            var leftCenter = new Vector2(body.Min.X + pad + leftWidth * 0.5f, body.Center.Y);
            var rightCenter = new Vector2(body.Max.X - pad - rightWidth * 0.5f, body.Center.Y);
            return new Layout(screen, leftCenter, rightCenter, key,
                new Vector2(screen.Center.X, screen.Max.Y + tipHeight * 0.5f));
        }

        var portraitKey = MathF.Min(KeySize * scale, (body.Width - pad * 4f) / 5.5f);
        var bandHeight = portraitKey * 4f + gap * 3f + pad * 2f;
        var portraitScreenWidth = body.Width - pad * 2f;
        var portraitScreenHeight = MathF.Min(portraitScreenWidth / ScreenAspect, body.Height - bandHeight - tipHeight - pad * 2f);
        portraitScreenWidth = portraitScreenHeight * ScreenAspect;
        var portraitScreenMin = new Vector2(body.Center.X - portraitScreenWidth * 0.5f, body.Min.Y + pad);
        var portraitScreen = new Rect(portraitScreenMin, portraitScreenMin + new Vector2(portraitScreenWidth, portraitScreenHeight));
        var bandTop = portraitScreen.Max.Y + tipHeight;
        var bandCenterY = bandTop + (body.Max.Y - bandTop) * 0.5f;
        return new Layout(portraitScreen,
            new Vector2(body.Min.X + pad + (portraitKey * 3f + gap * 2f) * 0.5f, bandCenterY),
            new Vector2(body.Max.X - pad - (portraitKey * 2f + gap) * 0.5f, bandCenterY), portraitKey,
            new Vector2(portraitScreen.Center.X, portraitScreen.Max.Y + tipHeight * 0.5f));
    }

    private void DrawGame(in GameContext context, Rect body, PhoneTheme theme, float scale)
    {
        var active = runtime!;
        var layout = Measure(body, scale);
        var running = context.DeltaSeconds > 0f;
        active.Muted = !running;
        var keyboard = running && GameInput.Claim();
        ReadControls(active, layout, theme, keyboard, scale);
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
        var pad = Pad * scale * 0.75f;
        var frame = new Rect(layout.Screen.Min - new Vector2(pad, pad), layout.Screen.Max + new Vector2(pad, pad));
        GameScene.Arena(drawList, frame, 12f * scale, scale, Accent);
        active.Present(drawList, layout.Screen);
        Typography.DrawCentered(drawList, layout.TipCenter, Loc.T(L.Games.DoomControls), theme.TextMuted, TextStyles.Caption1);
        tipProgress = GameBanner.Advance(tipProgress, context.DeltaSeconds, TipToastSeconds);
        GameBanner.Draw(drawList, new Vector2(layout.Screen.Center.X, layout.Screen.Min.Y + layout.Screen.Height * 0.18f),
            Loc.T(L.Games.DoomControls), Accent, theme, tipProgress, TextStyles.Subheadline);
    }

    private void ReadControls(DoomRuntime active, in Layout layout, PhoneTheme theme, bool keyboard, float scale)
    {
        var input = active.Input;
        var key = layout.Key;
        var gap = KeyGap * scale;
        var half = key * 0.5f;
        var size = new Vector2(key, key);
        var left = layout.LeftCenter;
        var forwardMin = new Vector2(left.X - half, left.Y - half - key - gap);
        var backMin = new Vector2(left.X - half, left.Y + half + gap);
        var strafeLeftMin = new Vector2(left.X - half - key - gap, left.Y - half);
        var strafeRightMin = new Vector2(left.X + half + gap, left.Y - half);
        var padForward = HeldKey(forwardMin, forwardMin + size, "W", theme, scale, out _);
        var padBack = HeldKey(backMin, backMin + size, "S", theme, scale, out _);
        var padStrafeLeft = HeldKey(strafeLeftMin, strafeLeftMin + size, "A", theme, scale, out _);
        var padStrafeRight = HeldKey(strafeRightMin, strafeRightMin + size, "D", theme, scale, out _);
        var right = layout.RightCenter;
        var wide = new Vector2(key * 2f + gap, key);
        var columnLeft = right.X - key - gap * 0.5f;
        var rowTop = right.Y - key * 2f - gap * 1.5f;
        var turnLeftMin = new Vector2(columnLeft, rowTop);
        var turnRightMin = new Vector2(right.X + gap * 0.5f, rowTop);
        var fireMin = new Vector2(columnLeft, rowTop + key + gap);
        var useMin = new Vector2(columnLeft, rowTop + (key + gap) * 2f);
        var menuMin = new Vector2(columnLeft, rowTop + (key + gap) * 3f);
        var padTurnLeft = HeldKey(turnLeftMin, turnLeftMin + size, "<", theme, scale, out _);
        var padTurnRight = HeldKey(turnRightMin, turnRightMin + size, ">", theme, scale, out _);
        var padFire = HeldKey(fireMin, fireMin + wide, Loc.T(L.Games.DoomFire), theme, scale, out var firePressed);
        var padUse = HeldKey(useMin, useMin + wide, Loc.T(L.Games.DoomUse), theme, scale, out var usePressed);
        HeldKey(menuMin, menuMin + wide, Loc.T(L.Games.DoomMenu), theme, scale, out var menuPressed);
        input.SetHeld(DoomAction.Forward, padForward || (keyboard && (ImGui.IsKeyDown(ImGuiKey.W) || ImGui.IsKeyDown(ImGuiKey.UpArrow))));
        input.SetHeld(DoomAction.Backward, padBack || (keyboard && (ImGui.IsKeyDown(ImGuiKey.S) || ImGui.IsKeyDown(ImGuiKey.DownArrow))));
        input.SetHeld(DoomAction.StrafeLeft, padStrafeLeft || (keyboard && ImGui.IsKeyDown(ImGuiKey.A)));
        input.SetHeld(DoomAction.StrafeRight, padStrafeRight || (keyboard && ImGui.IsKeyDown(ImGuiKey.D)));
        input.SetHeld(DoomAction.TurnLeft, padTurnLeft || (keyboard && ImGui.IsKeyDown(ImGuiKey.LeftArrow)));
        input.SetHeld(DoomAction.TurnRight, padTurnRight || (keyboard && ImGui.IsKeyDown(ImGuiKey.RightArrow)));
        input.SetHeld(DoomAction.Fire, padFire || (keyboard && (ImGui.IsKeyDown(ImGuiKey.Space) || ImGui.IsKeyDown(ImGuiKey.LeftCtrl))));
        input.SetHeld(DoomAction.Use, padUse || (keyboard && (ImGui.IsKeyDown(ImGuiKey.E) || ImGui.IsKeyDown(ImGuiKey.LeftShift))));
        for (var weapon = 0; weapon < 7; weapon++)
        {
            input.SetWeapon(weapon, keyboard && ImGui.IsKeyDown(ImGuiKey.Key1 + weapon));
        }

        if (menuPressed)
        {
            input.Tap(active.Doom, DoomKey.Escape);
        }

        if (active.InMenu)
        {
            if (firePressed)
            {
                input.Tap(active.Doom, DoomKey.Enter);
            }

            if (usePressed)
            {
                input.Tap(active.Doom, DoomKey.Escape);
            }
        }

        ReadDrag(input, layout.Screen);
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

    private bool HeldKey(Vector2 min, Vector2 max, string glyph, PhoneTheme theme, float scale, out bool pressed)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(min, max);
        var held = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        pressed = hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        var radius = (max.Y - min.Y) * 0.28f;
        Material.Frosted(drawList, min, max, radius, scale, held ? 1f : 0.85f);
        if (held)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.32f }));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.9f }), 1.5f * scale);
        }
        else if (hovered)
        {
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Accent with { W = 0.45f }), 1f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.DrawCentered(drawList, (min + max) * 0.5f, glyph, held ? Accent : theme.TextStrong,
            TextStyles.Headline.Scale, TextStyles.Headline.Weight);
        return held;
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
