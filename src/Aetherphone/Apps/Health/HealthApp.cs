using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp : IPhoneApp
{
    private const float RowHeight = 56f;
    private const float CompactRowHeight = 44f;
    private const float CardPadding = 8f;
    private const float CardRounding = 18f;
    private const float CardGap = 12f;
    private const float TileSize = 30f;

    private static string[] Tabs => new[]
    {
        Loc.T(L.Health.TabOverview), Loc.T(L.Health.TabActivity), Loc.T(L.Health.TabWater),
        Loc.T(L.Health.TabGoals), Loc.T(L.Health.TabHistory), Loc.T(L.Health.TabProfile),
    };

    public string Id => "health";
    public string DisplayName => Loc.T(L.Health.Title);
    public string Glyph => "He";
    public int BadgeCount => 0;

    private readonly HealthTracker tracker;
    private readonly GameData gameData;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Health);

    private int screenIndex;
    private float groupPad;

    // Editable UI buffers
    private string weightBuffer = string.Empty;
    private string customDrinkName = string.Empty;
    private int customDrinkMl = 250;
    private string? editingGoalId;
    private string goalNameBuffer = string.Empty;

    public HealthApp(HealthTracker tracker, GameData gameData, ConfirmService confirm)
    {
        this.tracker = tracker;
        this.gameData = gameData;
        this.confirm = confirm;
    }

    private HealthProfile Profile => tracker.Profile;
    private HealthUnits U => tracker.Profile.Units;
    private AppPalette Pal => AppPalettes.Health;

    public void OnOpened() => tracker.RefreshHeight();

    public void OnClosed() => tracker.SaveNow();

    public void Dispose()
    {
    }

    public void Draw(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        ui.Theme = theme;
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        ui.Backdrop(screen);

        var title = Loc.T(tracker.IsTracking && !Profile.SetupCompleted ? L.Health.Welcome : L.Health.Title);
        var rowCenterY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, rowCenterY), title, Pal.TitleInk, 1.15f,
            FontWeight.SemiBold);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        if (!tracker.IsTracking)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Health.LogInPrompt), Pal.MutedInk);
            return;
        }

        if (!Profile.SetupCompleted)
        {
            using (AppSurface.Begin(body))
            {
                DrawSetup(scale);
            }

            return;
        }

        DrawTabs(body, scale);
        var tabbed = new Rect(new Vector2(body.Min.X, body.Min.Y + 40f * scale), body.Max);
        using (AppSurface.Begin(tabbed))
        {
            switch (screenIndex)
            {
                case 0: DrawOverview(scale); break;
                case 1: DrawActivity(scale); break;
                case 2: DrawHydration(scale); break;
                case 3: DrawGoals(scale); break;
                case 4: DrawHistory(scale); break;
                default: DrawProfile(scale); break;
            }

            ImGui.Dummy(new Vector2(0f, 16f * scale));
        }
    }

    private void DrawTabs(Rect body, float scale)
    {
        var rect = new Rect(new Vector2(body.Min.X + 10f * scale, body.Min.Y + 4f * scale),
            new Vector2(body.Max.X - 10f * scale, body.Min.Y + 36f * scale));
        screenIndex = SegmentStrip.Draw("health.tabs", rect, Tabs, screenIndex, Pal);
    }

    // ---- Overview -----------------------------------------------------------

    private void DrawOverview(float scale)
    {
        var day = tracker.Profile.LatestDay ?? new HealthDay();
        var steps = HealthFormat.Steps(day.OnFootYalms, Profile.StrideYalms);

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 26f * scale),
            HealthFormat.Number(steps), Pal.TitleInk, TextStyles.LargeTitle);
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 54f * scale),
            Loc.T(L.Health.StepsTodayCaption, HealthFormat.Number(Profile.DailyStepGoal)), Pal.MutedInk,
            TextStyles.Footnote);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 74f * scale));

        var card = BeginCard(4, RowHeight, scale);
        StatRow(CardRow(card, 0, RowHeight, scale), Accent1, FontAwesomeIcon.Walking, Loc.T(L.Health.OnFoot),
            Dist(day.OnFootYalms), scale);
        StatRow(CardRow(card, 1, RowHeight, scale), Accent2, FontAwesomeIcon.Clock, Loc.T(L.Health.ActiveTime),
            HealthFormat.Duration(day.ActiveSeconds), scale);
        var kcal = Profile.CaloriesEnabled && Profile.WeightKg is not null
            ? Loc.T(L.Health.Kcal, day.Calories.ToString("0", Loc.Culture))
            : "—";
        StatRow(CardRow(card, 2, RowHeight, scale), Accent3, FontAwesomeIcon.Fire, Loc.T(L.Health.EstEnergy), kcal,
            scale);
        StatRow(CardRow(card, 3, RowHeight, scale), Accent4, FontAwesomeIcon.Tint, Loc.T(L.Health.Hydration),
            $"{day.DrinkCount} / {Profile.DailyHydrationGoal}", scale);
        EndCard(card, scale);

        ui.SectionLabel(Loc.T(L.Health.GoalsSection), TextStyles.FootnoteEmphasized, 6f);
        var shown = 0;
        for (var index = 0; index < Profile.Goals.Count && shown < 3; index++)
        {
            if (!Profile.Goals[index].Enabled)
            {
                continue;
            }

            GoalBar(Profile.Goals[index], scale);
            shown++;
        }

        if (shown == 0)
        {
            ui.HelpText(Loc.T(L.Health.NoActiveGoals));
        }

        ui.SectionLabel(Loc.T(L.Health.Streak), TextStyles.FootnoteEmphasized, 6f);
        var streakCard = BeginCard(1, RowHeight, scale);
        StatRow(CardRow(streakCard, 0, RowHeight, scale), Accent1, FontAwesomeIcon.Fire,
            Loc.T(L.Health.CurrentStreak),
            Profile.StreakDays == 1 ? Loc.T(L.Health.StreakOneDay) : Loc.T(L.Health.StreakDays, Profile.StreakDays),
            scale);
        EndCard(streakCard, scale);
    }

    // ---- Activity -----------------------------------------------------------

    private void DrawActivity(float scale)
    {
        var day = Profile.LatestDay ?? new HealthDay();
        ui.SectionLabel(Loc.T(L.Health.Today), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(4, CompactRowHeight, scale);
        StatRow(CardRow(card, 0, CompactRowHeight, scale), Accent1, FontAwesomeIcon.Walking, Loc.T(L.Health.OnFoot),
            Dist(day.OnFootYalms), scale);
        StatRow(CardRow(card, 1, CompactRowHeight, scale), Accent2, FontAwesomeIcon.Swimmer, Loc.T(L.Health.Swimming),
            Dist(day.SwimYalms), scale);
        StatRow(CardRow(card, 2, CompactRowHeight, scale), Accent3, FontAwesomeIcon.Water, Loc.T(L.Health.Diving),
            Dist(day.DiveYalms), scale);
        StatRow(CardRow(card, 3, CompactRowHeight, scale), Accent4, FontAwesomeIcon.Clock, Loc.T(L.Health.ActiveTime),
            HealthFormat.Duration(day.ActiveSeconds), scale);
        EndCard(card, scale);

        ui.SectionLabel(Loc.T(L.Health.Session), TextStyles.FootnoteEmphasized, 6f);
        var session = BeginCard(4, CompactRowHeight, scale);
        StatRow(CardRow(session, 0, CompactRowHeight, scale), Accent1, FontAwesomeIcon.Walking, Loc.T(L.Health.OnFoot),
            Dist(tracker.SessionOnFootYalms), scale);
        StatRow(CardRow(session, 1, CompactRowHeight, scale), Accent2, FontAwesomeIcon.Swimmer, Loc.T(L.Health.Swimming),
            Dist(tracker.SessionSwimmingYalms), scale);
        StatRow(CardRow(session, 2, CompactRowHeight, scale), Accent3, FontAwesomeIcon.Water, Loc.T(L.Health.Diving),
            Dist(tracker.SessionDivingYalms), scale);
        StatRow(CardRow(session, 3, CompactRowHeight, scale), Accent4, FontAwesomeIcon.Clock, Loc.T(L.Health.ActiveTime),
            HealthFormat.Duration(tracker.SessionActiveSeconds), scale);
        EndCard(session, scale);

        ui.SectionLabel(Loc.T(L.Health.AllTime), TextStyles.FootnoteEmphasized, 6f);
        var all = BeginCard(4, CompactRowHeight, scale);
        StatRow(CardRow(all, 0, CompactRowHeight, scale), Accent1, FontAwesomeIcon.Walking, Loc.T(L.Health.OnFoot),
            Dist(Profile.AllWalkYalms + Profile.AllRunYalms), scale);
        StatRow(CardRow(all, 1, CompactRowHeight, scale), Accent2, FontAwesomeIcon.Swimmer, Loc.T(L.Health.Swimming),
            Dist(Profile.AllSwimYalms), scale);
        StatRow(CardRow(all, 2, CompactRowHeight, scale), Accent3, FontAwesomeIcon.Water, Loc.T(L.Health.Diving),
            Dist(Profile.AllDiveYalms), scale);
        StatRow(CardRow(all, 3, CompactRowHeight, scale), Accent4, FontAwesomeIcon.LocationArrow, Loc.T(L.Health.Teleports),
            HealthFormat.Number(Profile.AllTeleports), scale);
        EndCard(all, scale);

        ui.SectionLabel(Loc.T(L.Health.Teleports), TextStyles.FootnoteEmphasized, 6f);
        var tp = BeginCard(2, CompactRowHeight, scale);
        StatRow(CardRow(tp, 0, CompactRowHeight, scale), Accent3, FontAwesomeIcon.LocationArrow, Loc.T(L.Health.Today),
            HealthFormat.Number((Profile.LatestDay?.Teleports ?? 0)), scale);
        StatRow(CardRow(tp, 1, CompactRowHeight, scale), Accent3, FontAwesomeIcon.Route, Loc.T(L.Health.DistanceSkipped),
            Dist(Profile.AllTeleportYalms), scale);
        EndCard(tp, scale);
        ui.HelpText(Loc.T(L.Health.TeleportHint));

        ui.SectionLabel(Loc.T(L.Health.Records), TextStyles.FootnoteEmphasized, 6f);
        var rec = BeginCard(3, CompactRowHeight, scale);
        StatRow(CardRow(rec, 0, CompactRowHeight, scale), Accent1, FontAwesomeIcon.Trophy, Loc.T(L.Health.MostStepsInDay),
            HealthFormat.Number(Profile.RecordStepsInDay), scale);
        StatRow(CardRow(rec, 1, CompactRowHeight, scale), Accent2, FontAwesomeIcon.Trophy, Loc.T(L.Health.LongestOnFootSession),
            HealthFormat.Duration(Profile.LongestOnFootSessionSeconds), scale);
        StatRow(CardRow(rec, 2, CompactRowHeight, scale), Accent2, FontAwesomeIcon.Trophy, Loc.T(L.Health.LongestSwimSession),
            HealthFormat.Duration(Profile.LongestSwimSessionSeconds), scale);
        EndCard(rec, scale);
    }

    // ---- Shared drawing helpers --------------------------------------------

    private Vector4 Accent1 => Pal.Accent;
    private static readonly Vector4 Accent2 = new(0.28f, 0.68f, 0.92f, 1f);
    private static readonly Vector4 Accent3 = new(0.62f, 0.52f, 0.96f, 1f);
    private static readonly Vector4 Accent4 = new(0.30f, 0.78f, 0.52f, 1f);

    private string Dist(double yalms) => HealthFormat.Distance(yalms, U);

    private Rect BeginCard(int rowCount, float rowHeight, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = (rowCount * rowHeight + 2f * CardPadding) * scale;
        var rect = new Rect(origin, origin + new Vector2(width, height));
        ui.Card(ImGui.GetWindowDrawList(), rect.Min, rect.Max, CardRounding * scale, elevated: true);
        return rect;
    }

    private static Rect CardRow(Rect card, int rowIndex, float rowHeight, float scale)
    {
        var padding = 14f * scale;
        var top = card.Min.Y + CardPadding * scale + rowIndex * rowHeight * scale;
        return new Rect(new Vector2(card.Min.X + padding, top),
            new Vector2(card.Max.X - padding, top + rowHeight * scale));
    }

    private static void EndCard(Rect card, float scale)
    {
        ImGui.SetCursorScreenPos(card.Min);
        ImGui.Dummy(card.Size);
        ImGui.Dummy(new Vector2(0f, CardGap * scale));
    }

    private void StatRow(Rect row, Vector4 tint, FontAwesomeIcon icon, string label, string value, float scale)
    {
        var tile = TileSize * scale;
        IconTile.Draw(new Vector2(row.Min.X + tile * 0.5f, row.Center.Y), tile, tint, icon);
        var textLeft = row.Min.X + tile + 12f * scale;
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, row.Center.Y - labelSize.Y * 0.5f), label, Pal.TitleInk,
            TextStyles.Headline);
        var valueSize = Typography.Measure(value, 1.02f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, Pal.TitleInk,
            1.02f, FontWeight.SemiBold);
    }

    private void GoalBar(HealthGoal goal, float scale)
    {
        var (current, target) = tracker.GoalProgress(goal);
        var fraction = target > 0 ? (float)Math.Clamp(current / target, 0d, 1d) : 0f;
        var complete = current + 0.0001 >= target;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 46f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var label = goal.Name;
        Typography.Draw(new Vector2(origin.X, origin.Y + 2f * scale), label,
            complete ? Accent4 : Pal.TitleInk, TextStyles.Subheadline);
        var pct = $"{(int)MathF.Round(fraction * 100f)}%";
        var pctSize = Typography.Measure(pct, TextStyles.Footnote);
        Typography.Draw(new Vector2(origin.X + width - pctSize.X, origin.Y + 2f * scale), pct, Pal.MutedInk,
            TextStyles.Footnote);

        var barTop = origin.Y + 26f * scale;
        var barMin = new Vector2(origin.X, barTop);
        var barMax = new Vector2(origin.X + width, barTop + 6f * scale);
        var rounding = (barMax.Y - barMin.Y) * 0.5f;
        drawList.AddRectFilled(barMin, barMax, ImGui.GetColorU32(Pal.FieldSurface), rounding);
        if (fraction > 0.001f)
        {
            drawList.AddRectFilled(barMin, new Vector2(barMin.X + width * fraction, barMax.Y),
                ImGui.GetColorU32(complete ? Accent4 : Pal.Accent), rounding);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    // Full-width pill button; returns true when clicked.
    private bool WideButton(string label, bool filled, float scale, float heightUnscaled = 40f)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var rect = new Rect(origin, origin + new Vector2(width, heightUnscaled * scale));
        var clicked = AppSkin.PillButton(rect, label, filled, true, ui.Theme);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, (heightUnscaled + 8f) * scale));
        return clicked;
    }
}
