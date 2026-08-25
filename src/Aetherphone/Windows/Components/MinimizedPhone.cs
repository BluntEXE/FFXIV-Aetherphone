using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal enum MinimizedAction : byte
{
    None,
    Expand,
    Close,
}

internal enum MinimizedActivity : byte
{
    None,
    Music,
    Call,
}

internal readonly struct MinimizedDrag
{
    public readonly Vector2 Delta;
    public readonly bool Released;

    public MinimizedDrag(Vector2 delta, bool released)
    {
        Delta = delta;
        Released = released;
    }
}

internal sealed class MinimizedPhone : IDisposable
{
    public const float BodyWidth = 82f;
    private const float MinBodyHeight = 156f;
    private const float TopPadding = 10f;
    private const float BottomPadding = 11f;
    private const float SidePadding = 4f;
    private const float SectionGap = 9f;
    private const float DateGap = 2f;
    private const float MusicHeight = 66f;
    private const float MusicExpandedHeight = 30f;
    private const float CallHeight = 32f;
    private const float CallExpandedHeight = 34f;
    private const float CardHeight = 60f;
    private const float BadgeHeight = 22f;
    private const float ClockMaxScale = 1.45f;
    private const float ClockMinScale = 0.95f;
    private const float DateScale = 0.72f;
    private const float HoldSeconds = 0.55f;
    private const float DragSlop = 5f;
    private const float CardHoldSeconds = 4.5f;
    private const float PulseSeconds = 0.8f;
    private const float TooltipClearance = 44f;
    private const int MaxQueuedCards = 3;
    private const float PresenceSmoothTime = 0.16f;
    private const float HoverSmoothTime = 0.12f;
    private const float ExpandSmoothTime = 0.17f;
    private const float CardSmoothTime = 0.20f;
    private const float HoldSmoothTime = 0.07f;
    private const float ControlThreshold = 0.6f;
    private const string MusicAppId = "music";
    private const string CallAppId = "message";
    private static readonly TimeSpan ShowingGrace = TimeSpan.FromSeconds(0.5);

    private readonly PlaybackHub playback;
    private readonly CallHub calls;
    private readonly NotificationService notifications;
    private readonly NotificationRouter router;
    private readonly INavigator navigation;
    private readonly Configuration configuration;
    private readonly Queue<PhoneNotification> queuedCards = new();
    private Spring hover;
    private Spring expand;
    private Spring activity;
    private Spring badge;
    private Spring dnd;
    private Spring card;
    private Spring hold;
    private MinimizedActivity shownActivity = MinimizedActivity.None;
    private PhoneNotification? cardNotification;
    private bool cardDismissed;
    private float cardElapsed;
    private float clock;
    private float pulseRemaining;
    private Vector4 pulseAccent;
    private bool pressed;
    private bool dragging;
    private bool holdFired;
    private float held;
    private Vector2 pressOrigin;
    private Vector2 dragDelta;
    private bool dragReleased;
    private bool activityHovered;
    private bool cardHovered;
    private string? badgeAppId;
    private Vector4 badgeAccent;
    private string countLabel = string.Empty;
    private int countValue = -1;
    private string durationLabel = string.Empty;
    private int durationSeconds = -1;
    private string timeLabel = string.Empty;
    private string meridiemLabel = string.Empty;
    private string dateLabel = string.Empty;
    private int timeKey = -1;
    private int timeFormat = -1;
    private int dateKey = -1;
    private CultureInfo? textCulture;
    private float clockScale;
    private Vector2 clockSize;
    private Vector2 dateSize;
    private int textFrame = -1;
    private DateTime lastInteractiveDrawUtc = DateTime.MinValue;

    public MinimizedPhone(PlaybackHub playback, CallHub calls, NotificationService notifications,
        NotificationRouter router, INavigator navigation, Configuration configuration)
    {
        this.playback = playback;
        this.calls = calls;
        this.notifications = notifications;
        this.router = router;
        this.navigation = navigation;
        this.configuration = configuration;
        notifications.Changed += RefreshBadge;
        notifications.Presented += OnPresented;
        notifications.Vibration += OnVibration;
        RefreshBadge();
    }

    public bool IsShowing => DateTime.UtcNow - lastInteractiveDrawUtc < ShowingGrace;

    public Vector2 Measure(float scale)
    {
        var band = ChassisGeometry.PuckBand(BodyWidth * scale);
        var height = MathF.Max(MinBodyHeight * scale - band, ContentHeight(scale)) + band;
        return new Vector2(MathF.Round(BodyWidth * scale), MathF.Round(height));
    }

    public static Vector2 IdleSize(float scale) =>
        new(MathF.Round(BodyWidth * scale), MathF.Round(MinBodyHeight * scale));

    public MinimizedDrag ConsumeDrag()
    {
        var result = new MinimizedDrag(dragDelta, dragReleased);
        dragDelta = Vector2.Zero;
        dragReleased = false;
        return result;
    }

    public MinimizedAction Draw(Rect body, PhoneTheme theme, float delta)
    {
        var scale = UiScale.Global;
        var geometry = ChassisGeometry.Puck(body);
        var dl = ImGui.GetForegroundDrawList();
        var lift = Math.Clamp(hover.Value, 0f, 1f);
        Elevation.Squircle(dl, geometry.Body.Min, geometry.Body.Max, geometry.BodyRadius, scale, 0.85f + 0.35f * lift);
        DeviceChrome.DrawShell(dl, geometry, scale, theme, 1f);
        return DrawFace(dl, geometry, theme, delta, true, 1f);
    }

    public MinimizedAction DrawFace(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneTheme theme, float delta,
        bool interactive, float alpha)
    {
        clock += delta;
        if (interactive)
        {
            lastInteractiveDrawUtc = DateTime.UtcNow;
        }

        var scale = UiScale.Global;
        var body = geometry.Body;
        var bodyHovered = interactive && UiInteract.Hover(body.Min, body.Max);
        var view = calls.Snapshot();
        StepState(delta, interactive, bodyHovered, view);
        if (alpha <= 0.001f)
        {
            return MinimizedAction.None;
        }

        RefreshText(scale);
        var screen = geometry.Screen;
        var expandEased = Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f));
        var cardEased = Easing.SmoothStep(Math.Clamp(card.Value, 0f, 1f));
        var activityValue = Math.Clamp(activity.Value, 0f, 1f);
        var hoveredControl = false;
        dl.PushClipRect(screen.Min, screen.Max, true);
        var y = MinimizedPhoneRenderer.DrawClockBlock(dl, screen, screen.Min.Y + TopPadding * scale, timeLabel,
            meridiemLabel, dateLabel, clockScale, clockSize, dateSize, theme, alpha, Math.Clamp(dnd.Value, 0f, 1f),
            scale);
        activityHovered = false;
        if (activityValue > 0.01f && shownActivity != MinimizedActivity.None)
        {
            y = DrawActivity(dl, screen, y, theme, view, scale, alpha, activityValue, expandEased, interactive,
                bodyHovered, out hoveredControl);
        }

        cardHovered = false;
        if (cardEased > 0.01f && cardNotification is { } notification)
        {
            y = DrawCard(dl, screen, y, notification, theme, scale, alpha, cardEased, bodyHovered);
        }

        var badgeValue = Math.Clamp(badge.Value, 0f, 1f);
        if (badgeValue > 0.01f && badgeAppId is { } appId)
        {
            var center = new Vector2(screen.Center.X, screen.Max.Y - (BottomPadding + BadgeHeight * 0.5f) * scale);
            MinimizedPhoneRenderer.DrawBadge(dl, center, appId, badgeAccent, countLabel, theme, alpha * badgeValue,
                scale);
        }

        var holdValue = Math.Clamp(hold.Value, 0f, 1f);
        if (holdValue > 0.005f)
        {
            MinimizedPhoneRenderer.DrawHoldSweep(dl, geometry, theme, holdValue * alpha, scale);
        }

        dl.PopClipRect();
        if (cardEased > 0.01f && cardNotification is { } stroked)
        {
            MinimizedPhoneRenderer.DrawCardStroke(dl, geometry, stroked.Accent, alpha * cardEased, cardHovered, scale);
        }

        if (pulseRemaining > 0f)
        {
            var strength = pulseRemaining / PulseSeconds;
            MinimizedPhoneRenderer.DrawPulse(dl, geometry, pulseAccent, strength * strength * alpha, scale);
        }

        if (!interactive)
        {
            return MinimizedAction.None;
        }

        return HandleGesture(body, scale, delta, bodyHovered, hoveredControl);
    }

    private float DrawActivity(ImDrawListPtr dl, Rect screen, float y, PhoneTheme theme, in CallView view,
        float scale, float alpha, float activityValue, float expandEased, bool interactive, bool bodyHovered,
        out bool hoveredControl)
    {
        hoveredControl = false;
        var sectionTop = y + SectionGap * scale * activityValue;
        var compactHeight = ActivityHeight(shownActivity) * scale;
        var expandedHeight = ExpandedHeight(shownActivity) * scale * expandEased;
        var fullHeight = (compactHeight + expandedHeight) * activityValue;
        var section = new Rect(new Vector2(screen.Min.X + SidePadding * scale, sectionTop),
            new Vector2(screen.Max.X - SidePadding * scale, sectionTop + fullHeight));
        activityHovered = bodyHovered && activityValue > 0.9f && UiInteract.Hover(section.Min, section.Max);
        dl.PushClipRect(section.Min, section.Max, true);
        var compact = new Rect(section.Min, new Vector2(section.Max.X, sectionTop + compactHeight));
        var sectionAlpha = alpha * activityValue;
        if (shownActivity == MinimizedActivity.Music)
        {
            MinimizedPhoneRenderer.DrawMusicSection(dl, compact, playback, clock, sectionAlpha, scale, theme);
        }
        else
        {
            MinimizedPhoneRenderer.DrawCallSection(dl, compact, view, DurationLabel(view), clock, sectionAlpha, scale,
                theme);
        }

        if (expandedHeight > 0.5f)
        {
            var row = new Rect(new Vector2(section.Min.X, compact.Max.Y),
                new Vector2(section.Max.X, compact.Max.Y + expandedHeight));
            var active = interactive && expandEased > ControlThreshold;
            var rowAlpha = sectionAlpha * expandEased;
            if (shownActivity == MinimizedActivity.Music)
            {
                var result = MinimizedPhoneRenderer.DrawMusicTransport(dl, row, playback, theme, rowAlpha, active,
                    scale);
                ApplyMusicControl(result.Action);
                hoveredControl = result.Hovered;
            }
            else
            {
                var result = MinimizedPhoneRenderer.DrawCallControls(dl, row, view, theme, rowAlpha, active, scale);
                ApplyCallControl(result.Action);
                hoveredControl = result.Hovered;
            }
        }

        dl.PopClipRect();
        return sectionTop + fullHeight;
    }

    private float DrawCard(ImDrawListPtr dl, Rect screen, float y, PhoneNotification notification, PhoneTheme theme,
        float scale, float alpha, float cardEased, bool bodyHovered)
    {
        var sectionTop = y + SectionGap * scale * cardEased;
        var height = CardHeight * scale * cardEased;
        var section = new Rect(new Vector2(screen.Min.X + SidePadding * scale, sectionTop),
            new Vector2(screen.Max.X - SidePadding * scale, sectionTop + height));
        cardHovered = bodyHovered && cardEased > 0.9f && UiInteract.Hover(section.Min, section.Max);
        dl.PushClipRect(section.Min, section.Max, true);
        var full = new Rect(section.Min, new Vector2(section.Max.X, sectionTop + CardHeight * scale));
        MinimizedPhoneRenderer.DrawCardSection(dl, full, notification, theme, alpha * cardEased, scale);
        dl.PopClipRect();
        return sectionTop + height;
    }

    private void StepState(float delta, bool interactive, bool bodyHovered, in CallView view)
    {
        if (pulseRemaining > 0f)
        {
            pulseRemaining = MathF.Max(0f, pulseRemaining - delta);
        }

        var callActive = view.State is CallState.Dialing or CallState.Connecting or CallState.Active;
        var current = callActive ? MinimizedActivity.Call :
            playback.IsActive ? MinimizedActivity.Music : MinimizedActivity.None;
        if (current != MinimizedActivity.None)
        {
            shownActivity = current;
        }

        activity.Step(current == MinimizedActivity.None ? 0f : 1f, PresenceSmoothTime, delta);
        badge.Step(badgeAppId is null ? 0f : 1f, PresenceSmoothTime, delta);
        dnd.Step(configuration.DoNotDisturb ? 1f : 0f, PresenceSmoothTime, delta);
        AdvanceCard(delta, bodyHovered);
        hover.Step(interactive && bodyHovered ? 1f : 0f, HoverSmoothTime, delta);
        var wantsExpand = interactive && bodyHovered && current != MinimizedActivity.None && !dragging;
        expand.Step(wantsExpand ? 1f : 0f, ExpandSmoothTime, delta);
        var holdTarget = pressed && !dragging ? Math.Clamp(held / HoldSeconds, 0f, 1f) : 0f;
        hold.Step(holdTarget, HoldSmoothTime, delta);
        if (activity.Value < 0.01f && current == MinimizedActivity.None)
        {
            shownActivity = MinimizedActivity.None;
        }
    }

    private void AdvanceCard(float delta, bool bodyHovered)
    {
        if (cardNotification is null)
        {
            card.SnapTo(0f);
            if (queuedCards.Count > 0)
            {
                BeginCard(queuedCards.Dequeue());
            }

            return;
        }

        if (!cardDismissed)
        {
            card.Step(1f, CardSmoothTime, delta);
            if (!bodyHovered)
            {
                cardElapsed += delta;
            }

            if (cardElapsed >= CardHoldSeconds)
            {
                cardDismissed = true;
            }

            return;
        }

        card.Step(0f, CardSmoothTime, delta);
        if (card.Value > 0.02f)
        {
            return;
        }

        card.SnapTo(0f);
        cardNotification = null;
        cardDismissed = false;
    }

    private void ApplyMusicControl(MinimizedControl control)
    {
        switch (control)
        {
            case MinimizedControl.Previous:
                playback.Previous();
                break;
            case MinimizedControl.Next:
                playback.Next();
                break;
            case MinimizedControl.PlayPause:
                playback.TogglePlayPause();
                break;
        }
    }

    private void ApplyCallControl(MinimizedControl control)
    {
        if (control == MinimizedControl.ToggleMute)
        {
            calls.ToggleMute();
        }
        else if (control == MinimizedControl.Hangup)
        {
            calls.Hangup();
        }
    }

    private MinimizedAction HandleGesture(Rect body, float scale, float delta, bool bodyHovered, bool hoveredControl)
    {
        if (bodyHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!pressed && bodyHovered && !hoveredControl && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            pressed = true;
            dragging = false;
            holdFired = false;
            held = 0f;
            pressOrigin = ImGui.GetMousePos();
        }

        var action = MinimizedAction.None;
        if (pressed)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                var mouse = ImGui.GetMousePos();
                if (!dragging && (mouse - pressOrigin).Length() > DragSlop * scale)
                {
                    dragging = true;
                }

                if (dragging)
                {
                    dragDelta += ImGui.GetIO().MouseDelta;
                }
                else
                {
                    held += delta;
                    if (held >= HoldSeconds && !holdFired)
                    {
                        holdFired = true;
                        action = MinimizedAction.Close;
                    }
                }
            }
            else
            {
                if (dragging)
                {
                    dragReleased = true;
                }
                else if (!holdFired && bodyHovered)
                {
                    action = Tap();
                }

                pressed = false;
                dragging = false;
                held = 0f;
            }
        }

        if (!pressed && bodyHovered && !activityHovered && !cardHovered && expand.Value < 0.5f)
        {
            var viewport = ImGui.GetMainViewport();
            var side = body.Max.Y + TooltipClearance * scale > viewport.Pos.Y + viewport.Size.Y
                ? HoverLabelSide.Above
                : HoverLabelSide.Below;
            HoverTooltip.Show("minimized.phone", body, Loc.T(L.Plugin.MinimizedHint), side);
        }

        return action;
    }

    private MinimizedAction Tap()
    {
        if (cardHovered && cardNotification is { } notification && !cardDismissed)
        {
            router.Open(notification);
            cardDismissed = true;
            queuedCards.Clear();
            return MinimizedAction.Expand;
        }

        if (activityHovered && shownActivity == MinimizedActivity.Music)
        {
            navigation.Open(MusicAppId);
        }
        else if (activityHovered && shownActivity == MinimizedActivity.Call)
        {
            calls.RequestCallScreen();
            navigation.Open(CallAppId);
        }

        return MinimizedAction.Expand;
    }

    private float ContentHeight(float scale)
    {
        RefreshText(scale);
        var height = TopPadding * scale + clockSize.Y + DateGap * scale + dateSize.Y;
        if (shownActivity != MinimizedActivity.None)
        {
            var expandEased = Easing.SmoothStep(Math.Clamp(expand.Value, 0f, 1f));
            height += (SectionGap + ActivityHeight(shownActivity) + ExpandedHeight(shownActivity) * expandEased) *
                      scale * Math.Clamp(activity.Value, 0f, 1f);
        }

        height += (SectionGap + CardHeight) * scale * Easing.SmoothStep(Math.Clamp(card.Value, 0f, 1f));
        height += (SectionGap + BadgeHeight) * scale * Math.Clamp(badge.Value, 0f, 1f);
        return height + BottomPadding * scale;
    }

    private void RefreshText(float scale)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == textFrame)
        {
            return;
        }

        textFrame = frame;
        var now = DateTime.Now;
        var minuteKey = now.Hour * 60 + now.Minute;
        if (minuteKey != timeKey || timeFormat != TimeText.FormatVersion || !ReferenceEquals(textCulture, Loc.Culture))
        {
            timeKey = minuteKey;
            timeFormat = TimeText.FormatVersion;
            timeLabel = TimeText.HourLabel(now.Hour) + ":" + TimeText.MinuteLabel(now.Minute);
            meridiemLabel = TimeText.Use24Hour ? string.Empty : TimeText.MeridiemLabel(now.Hour >= 12);
        }

        var dayKey = now.Year * 400 + now.DayOfYear;
        if (dayKey != dateKey || !ReferenceEquals(textCulture, Loc.Culture))
        {
            dateKey = dayKey;
            dateLabel = now.ToString("ddd d", Loc.Culture);
        }

        textCulture = Loc.Culture;
        var textWidth = BodyWidth * scale - ChassisGeometry.PuckBand(BodyWidth * scale) - SidePadding * 2f * scale;
        clockScale = Typography.FitScale(timeLabel, textWidth, TextScale(ClockMaxScale), TextScale(ClockMinScale),
            FontWeight.Bold);
        clockSize = Typography.Measure(timeLabel, clockScale, FontWeight.Bold);
        dateSize = Typography.Measure(dateLabel, TextScale(DateScale), FontWeight.Regular);
    }

    private string DurationLabel(in CallView view)
    {
        if (view.State != CallState.Active)
        {
            durationSeconds = -1;
            return CallStatusText.Label(view);
        }

        if (view.Seconds != durationSeconds || !view.Connected)
        {
            durationSeconds = view.Seconds;
            durationLabel = CallStatusText.Label(view);
        }

        return durationLabel;
    }

    private static float ActivityHeight(MinimizedActivity kind) =>
        kind == MinimizedActivity.Call ? CallHeight : MusicHeight;

    private static float ExpandedHeight(MinimizedActivity kind) =>
        kind == MinimizedActivity.Call ? CallExpandedHeight : MusicExpandedHeight;

    private static float TextScale(float scale) => scale / UiScale.Phone;

    private void RefreshBadge()
    {
        var unread = notifications.UnreadCount;
        if (unread != countValue)
        {
            countValue = unread;
            countLabel = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        }

        var recent = notifications.Recent;
        for (var index = recent.Count - 1; index >= 0; index--)
        {
            var notification = recent[index];
            if (notification.Read)
            {
                continue;
            }

            badgeAppId = notification.AppId;
            badgeAccent = notification.Accent;
            return;
        }

        badgeAppId = null;
    }

    private void OnPresented(PhoneNotification notification)
    {
        if (!IsShowing)
        {
            return;
        }

        if (cardNotification is { } showing && !cardDismissed && showing.StackKey == notification.StackKey)
        {
            cardNotification = notification;
            cardElapsed = 0f;
            return;
        }

        RemoveQueuedGroup(notification.StackKey);
        if (queuedCards.Count >= MaxQueuedCards)
        {
            return;
        }

        if (cardNotification is null)
        {
            BeginCard(notification);
            return;
        }

        queuedCards.Enqueue(notification);
    }

    private void RemoveQueuedGroup(string stackKey)
    {
        var count = queuedCards.Count;
        for (var index = 0; index < count; index++)
        {
            var queued = queuedCards.Dequeue();
            if (queued.StackKey != stackKey)
            {
                queuedCards.Enqueue(queued);
            }
        }
    }

    private void BeginCard(PhoneNotification notification)
    {
        cardNotification = notification;
        cardDismissed = false;
        cardElapsed = 0f;
        card.SnapTo(0f);
    }

    private void OnVibration(PhoneNotification notification)
    {
        if (!IsShowing)
        {
            return;
        }

        pulseRemaining = PulseSeconds;
        pulseAccent = notification.Accent;
    }

    public void Dispose()
    {
        notifications.Changed -= RefreshBadge;
        notifications.Presented -= OnPresented;
        notifications.Vibration -= OnVibration;
    }
}
