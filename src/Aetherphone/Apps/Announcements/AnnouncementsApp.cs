using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Announcements;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Announcements;

internal sealed class AnnouncementsApp : IPhoneApp
{
    private const float RefreshSeconds = 30f;
    private const float CardPadding = 16f;
    private const float CardRounding = 18f;
    private const float CardGap = 12f;
    private const int MaxPreviewLines = 3;
    private const float UnreadDotRadius = 4f;
    private const float MeasureWidth = 10000f;

    public string Id => AnnouncementsStore.AppId;
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Announcements);
    public string Glyph => "An";
    public int BadgeCount => store.UnreadCount;

    private readonly AnnouncementsStore store;
    private readonly AnnouncementsLauncher launcher;
    private readonly AppSkin ui = new(AppPalettes.Announcements);
    private readonly ViewRouter<AnnouncementsRoute> router;
    private readonly RouterDraw<AnnouncementsRoute> drawView;
    private readonly Action back;

    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private float sinceRefresh;

    public AnnouncementsApp(AethernetSession session, AnnouncementsClient client, NotificationService notifications,
        Configuration configuration, AnnouncementsLauncher launcher)
    {
        store = new AnnouncementsStore(session, client, notifications, configuration);
        this.launcher = launcher;
        router = new ViewRouter<AnnouncementsRoute>(AnnouncementsRoute.List);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        sinceRefresh = 0f;
        if (launcher.TryConsumeDetail(out var announcementId))
        {
            router.Push(AnnouncementsRoute.Detail(announcementId), false);
        }

        store.Refresh();
        store.MarkAllSeen();
    }

    public void OnClosed()
    {
        router.Reset();
        store.MarkAllSeen();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = theme;

        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, theme, scale);
        ui.Backdrop(screen);

        if (!store.IsSignedIn)
        {
            ui.Body(context.Content);
            AppHeader.Draw(context, DisplayName, navigation.Back);
            var top = context.Content.Min.Y + AppHeader.Height * scale;
            var body = new Rect(new Vector2(context.Content.Min.X, top), context.Content.Max);
            Typography.DrawCentered(body.Center, Loc.T(L.Announcements.SignInRequired), ui.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        TickRefresh();
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(AnnouncementsRoute route, Rect area, int depth)
    {
        ui.Body(area);
        if (route.Screen == AnnouncementsScreen.Detail)
        {
            DrawDetail(area, route.Id!);
            return;
        }

        DrawList(area);
    }

    private void TickRefresh()
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh < RefreshSeconds || store.Loading)
        {
            return;
        }

        sinceRefresh = 0f;
        store.Refresh();
    }

    private void DrawList(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, DisplayName, navigation.Back);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        UiAnchors.Report("announcements.feed", body);

        var announcements = store.Announcements;
        if (announcements.Length == 0)
        {
            DrawEmptyState(body, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < announcements.Length; index++)
            {
                DrawCard(announcements[index], scale, index == 0);
                ImGui.Dummy(new Vector2(0f, CardGap * scale));
            }
        }
    }

    private void DrawEmptyState(Rect body, float scale)
    {
        if (store.Loading && !store.LoadedOnce)
        {
            LoadingPulse.Draw(new Vector2(body.Center.X, body.Min.Y + 120f * scale), 13f * scale, ui.Accent,
                ui.MutedInk, Loc.T(L.Common.Loading));
            return;
        }

        EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Announcements.EmptyTitle),
            Loc.T(L.Announcements.EmptyHint));
    }

    private void DrawCard(AnnouncementDto announcement, float scale, bool isFirstCard)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = CardPadding * scale;
        var unread = store.IsUnread(announcement);
        var text = AnnouncementText.For(announcement);

        var dotLane = unread ? (UnreadDotRadius * 2f + 8f) * scale : 0f;
        var titleLeft = origin.X + pad + dotLane;
        var titleWidth = width - pad * 2f - dotLane;
        var bodyWidth = width - pad * 2f;

        var titleHeight = Typography.MeasureWrappedBlock(text.Title, TextStyles.Headline, titleWidth).Y;
        var previewLines = PreviewLines(text.Body, bodyWidth);
        var previewLineHeight = LineHeight(TextStyles.Subheadline);
        var previewHeight = previewLines.Length * previewLineHeight;
        var stampHeight = Typography.Measure("Ag", TextStyles.Caption1).Y;

        var cardHeight = pad + titleHeight + 6f * scale + previewHeight + 10f * scale + stampHeight + pad;
        var cardMax = new Vector2(origin.X + width, origin.Y + cardHeight);
        var rounding = CardRounding * scale;
        ui.Card(drawList, origin, cardMax, rounding, true);
        if (isFirstCard)
        {
            UiAnchors.Report("announcements.card", new Rect(origin, cardMax));
        }

        var cursorY = origin.Y + pad;
        if (unread)
        {
            drawList.AddCircleFilled(
                new Vector2(origin.X + pad + UnreadDotRadius * scale, cursorY + LineHeight(TextStyles.Headline) * 0.5f),
                UnreadDotRadius * scale, ImGui.GetColorU32(ui.Accent), 16);
        }

        Typography.DrawWrappedLeft(new Vector2(titleLeft, cursorY), text.Title, ui.TitleInk, TextStyles.Headline,
            titleWidth);
        cursorY += titleHeight + 6f * scale;

        for (var index = 0; index < previewLines.Length; index++)
        {
            Typography.Draw(drawList, new Vector2(origin.X + pad, cursorY), previewLines[index], ui.BodyInk,
                TextStyles.Subheadline);
            cursorY += previewLineHeight;
        }

        Typography.Draw(new Vector2(origin.X + pad, cardMax.Y - pad - stampHeight),
            TimeText.Short(announcement.CreatedAtUnix), ui.MutedInk, TextStyles.Caption1);

        var hovered = ImGui.IsMouseHoveringRect(origin, cardMax);
        if (hovered)
        {
            Squircle.Fill(drawList, origin, cardMax, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(origin, cardMax, hovered))
        {
            router.Push(AnnouncementsRoute.Detail(announcement.Id));
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private void DrawDetail(Rect area, string announcementId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, DisplayName, back);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var announcement = Resolve(announcementId);
        if (announcement is null)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Announcements.UnavailableTitle),
                Loc.T(L.Announcements.UnavailableHint));
            return;
        }

        var text = AnnouncementText.For(announcement);
        using (AppSurface.Begin(body))
        {
            var pad = CardPadding * scale;
            ImGui.Dummy(new Vector2(0f, 6f * scale));
            var width = ScrollLayout.StableContentWidth();
            var contentWidth = width - pad * 2f;

            var titleOrigin = ImGui.GetCursorScreenPos();
            var titleHeight = Typography.DrawWrappedLeft(new Vector2(titleOrigin.X + pad, titleOrigin.Y), text.Title,
                ui.TitleInk, TextStyles.Title2, contentWidth);
            ImGui.Dummy(new Vector2(width, titleHeight));
            ImGui.Dummy(new Vector2(0f, 6f * scale));

            var stampOrigin = ImGui.GetCursorScreenPos();
            Typography.Draw(new Vector2(stampOrigin.X + pad, stampOrigin.Y), TimeText.Short(announcement.CreatedAtUnix),
                ui.MutedInk, TextStyles.Caption1);
            ImGui.Dummy(new Vector2(width, Typography.Measure("Ag", TextStyles.Caption1).Y));
            ImGui.Dummy(new Vector2(0f, 14f * scale));

            var bodyOrigin = ImGui.GetCursorScreenPos();
            var bodyHeight = Typography.DrawWrappedLeft(new Vector2(bodyOrigin.X + pad, bodyOrigin.Y), text.Body,
                ui.BodyInk, TextStyles.Body, contentWidth);
            ImGui.Dummy(new Vector2(width, bodyHeight));
            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private AnnouncementDto? Resolve(string announcementId)
    {
        var announcements = store.Announcements;
        for (var index = 0; index < announcements.Length; index++)
        {
            if (announcements[index].Id == announcementId)
            {
                return announcements[index];
            }
        }

        return null;
    }

    private static string[] PreviewLines(string body, float maxWidth)
    {
        var wrapped = Typography.WrapText(body, TextStyles.Subheadline, maxWidth);
        if (wrapped.Length <= MaxPreviewLines)
        {
            return wrapped;
        }

        var trimmed = new string[MaxPreviewLines];
        Array.Copy(wrapped, trimmed, MaxPreviewLines);
        trimmed[MaxPreviewLines - 1] = Typography.FitText(trimmed[MaxPreviewLines - 1] + "…", maxWidth,
            TextStyles.Subheadline);
        return trimmed;
    }

    private static float LineHeight(in TextStyle style) =>
        Typography.MeasureWrappedBlock("Ag", style, MeasureWidth).Y;

    public void Dispose()
    {
        store.Dispose();
    }
}
