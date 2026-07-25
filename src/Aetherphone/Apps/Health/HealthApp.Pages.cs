using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Health;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private static readonly string[] DrinkKinds = { "Water", "Tea", "Coffee", "Juice" };
    private static readonly string[] UnitLabels = { "Eorzean", "Metric", "Imperial" };
    private static readonly string[] ScopeLabels = { "Daily", "Weekly", "Session", "All-time" };

    // ---- Setup (stepped registration wizard) --------------------------------

    private const int SetupSteps = 5;
    private int setupStep;

    private static readonly string[] SetupSubtitles =
    {
        "Let's set up your adventurer's health profile.",
        "Choose your daily expedition goals.",
        "Optional fictional energy estimates.",
        "Tune how travel becomes estimated steps.",
        "Review your profile and begin.",
    };

    private void DrawSetup(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        DrawStepDots(scale);

        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 14f * scale), "Welcome, Adventurer!", Pal.TitleInk,
            TextStyles.Title2);
        Typography.DrawCentered(new Vector2(centerX, origin.Y + 40f * scale), SetupSubtitles[setupStep], Pal.MutedInk,
            TextStyles.Subheadline);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 60f * scale));

        switch (setupStep)
        {
            case 0: SetupUnits(scale); break;
            case 1: SetupGoals(scale); break;
            case 2: SetupEnergy(scale); break;
            case 3: SetupMovement(scale); break;
            default: SetupReview(scale); break;
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        SetupNav(scale);
    }

    private void SetupUnits(float scale)
    {
        DrawSummaryCard(scale);
        StepLabel("Preferred units", scale);
        if (RadioRow("Eorzean", "Yalms / Malms / Ponz", U == HealthUnits.Eorzean, scale))
        {
            SetUnits(HealthUnits.Eorzean);
        }

        if (RadioRow("Metric", "Metres / km / kg / ml", U == HealthUnits.Metric, scale))
        {
            SetUnits(HealthUnits.Metric);
        }

        if (RadioRow("Imperial", "Feet / miles / lb / fl oz", U == HealthUnits.Imperial, scale))
        {
            SetUnits(HealthUnits.Imperial);
        }
    }

    private void SetUnits(HealthUnits units)
    {
        if (Profile.Units == units)
        {
            return;
        }

        Profile.Units = units;
        tracker.SaveNow();
    }

    private void SetupGoals(float scale)
    {
        StepLabel("Daily goals", scale);
        var steps = IntField("Steps", "##hp.setup.steps", Profile.DailyStepGoal, 1000, 1000, 100000, scale);
        if (steps != Profile.DailyStepGoal)
        {
            Profile.DailyStepGoal = steps;
            tracker.MarkDirty();
        }

        var swim = FloatField("Swimming (yalms)", "##hp.setup.swim", Profile.DailySwimGoalYalms, 100, 100, 100000,
            "%.0f", scale);
        if (Math.Abs(swim - Profile.DailySwimGoalYalms) > 0.01)
        {
            Profile.DailySwimGoalYalms = swim;
            tracker.MarkDirty();
        }

        var drinks = IntField("Hydration (drinks)", "##hp.setup.drinks", Profile.DailyHydrationGoal, 1, 1, 20, scale);
        if (drinks != Profile.DailyHydrationGoal)
        {
            Profile.DailyHydrationGoal = drinks;
            tracker.MarkDirty();
        }
    }

    private void SetupEnergy(float scale)
    {
        StepLabel("Fictional energy", scale);
        ui.HelpText("Character weight is optional and used only for fictional activity-energy estimates.");
        ui.LabelValue("Current", Profile.WeightKg is { } kg ? HealthFormat.Weight(kg, U) : "not set");
        ui.Field($"Weight ({WeightUnitLabel()})", "##health.setup.weight", ref weightBuffer, 8, false);
        if (WideButton("Set weight", false, scale, 30f) &&
            double.TryParse(weightBuffer, NumberStyles.Any, CultureInfo.CurrentCulture, out var value) && value > 0)
        {
            Profile.WeightKg = HealthFormat.WeightToKg(value, U);
            tracker.SaveNow();
        }

        DrawWeightSuggestions(scale);

        var calories = Profile.CaloriesEnabled;
        ui.ToggleRow("Estimate activity energy", ref calories);
        if (calories != Profile.CaloriesEnabled)
        {
            Profile.CaloriesEnabled = calories;
            tracker.MarkDirty();
        }
    }

    private void SetupMovement(float scale)
    {
        StepLabel("Movement", scale);
        ui.LabelValue("Height", $"{HealthFormat.Height(tracker.HeightCm, U)} · {tracker.HeightSource}");
        var strideDelta = StepperRow("Yalms per step",
            Profile.StrideYalms.ToString("0.00", CultureInfo.CurrentCulture), scale);
        if (strideDelta != 0)
        {
            Profile.StrideYalms = Math.Clamp(Profile.StrideYalms + strideDelta * 0.05, 0.30, 1.50);
            tracker.MarkDirty();
        }

        if (WideButton("Suggest stride from height", false, scale, 30f))
        {
            Profile.StrideYalms = HealthFormat.SuggestStride(tracker.HeightCm);
            tracker.SaveNow();
        }

        ui.HelpText("Only walking and running produce estimated steps. Raw distance is stored, so changing stride never loses progress.");
    }

    private void SetupReview(float scale)
    {
        StepLabel("Review", scale);
        var card = BeginCard(6, 38f, scale);
        KeyRow(CardRow(card, 0, 38f, scale), "Units", UnitLabels[(int)U], scale);
        KeyRow(CardRow(card, 1, 38f, scale), "Steps goal", HealthFormat.Number(Profile.DailyStepGoal), scale);
        KeyRow(CardRow(card, 2, 38f, scale), "Swim goal", Dist(Profile.DailySwimGoalYalms), scale);
        KeyRow(CardRow(card, 3, 38f, scale), "Hydration goal", $"{Profile.DailyHydrationGoal} drinks", scale);
        KeyRow(CardRow(card, 4, 38f, scale), "Weight",
            Profile.WeightKg is { } kg ? HealthFormat.Weight(kg, U) : "not set", scale);
        KeyRow(CardRow(card, 5, 38f, scale), "Energy estimates", Profile.CaloriesEnabled ? "On" : "Off", scale);
        EndCard(card, scale);
        ui.HelpText("Health tracks fictional activity performed by your FFXIV character. Its values are estimates intended for roleplay and statistics.");
    }

    private void SetupNav(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 44f * scale;
        var gap = 10f * scale;
        var last = setupStep >= SetupSteps - 1;

        if (setupStep > 0)
        {
            var half = (width - gap) * 0.5f;
            var backRect = new Rect(origin, origin + new Vector2(half, height));
            var nextRect = new Rect(new Vector2(origin.X + half + gap, origin.Y),
                new Vector2(origin.X + width, origin.Y + height));
            if (AppSkin.PillButton(backRect, "Back", false, true, ui.Theme))
            {
                setupStep--;
            }

            if (AppSkin.PillButton(nextRect, last ? "Begin" : "Next", true, true, ui.Theme))
            {
                Advance(last);
            }
        }
        else
        {
            var nextRect = new Rect(origin, origin + new Vector2(width, height));
            if (AppSkin.PillButton(nextRect, "Next", true, true, ui.Theme))
            {
                Advance(false);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
    }

    private void Advance(bool finish)
    {
        if (finish)
        {
            Profile.SetupCompleted = true;
            if (Profile.Goals.Count == 0)
            {
                Profile.Goals = HealthTracker.DefaultGoals();
            }

            setupStep = 0;
            tracker.SaveNow();
            return;
        }

        setupStep = Math.Min(setupStep + 1, SetupSteps - 1);
    }

    private void DrawStepDots(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var radius = 12f * scale;
        var padX = radius + 6f * scale;
        var usable = width - 2f * padX;
        var cy = origin.Y + radius + 4f * scale;
        var accent = ImGui.GetColorU32(Pal.Accent);
        var idle = ImGui.GetColorU32(Pal.FieldSurface);

        for (var index = 0; index < SetupSteps - 1; index++)
        {
            var x0 = origin.X + padX + usable * (index / (float)(SetupSteps - 1));
            var x1 = origin.X + padX + usable * ((index + 1) / (float)(SetupSteps - 1));
            drawList.AddLine(new Vector2(x0, cy), new Vector2(x1, cy), index < setupStep ? accent : idle, 2f * scale);
        }

        for (var index = 0; index < SetupSteps; index++)
        {
            var cx = origin.X + padX + usable * (index / (float)(SetupSteps - 1));
            var center = new Vector2(cx, cy);
            var done = index <= setupStep;
            drawList.AddCircleFilled(center, radius, done ? accent : idle, 24);
            Typography.DrawCentered(center, (index + 1).ToString(CultureInfo.InvariantCulture),
                done ? new Vector4(1f, 1f, 1f, 1f) : Pal.MutedInk, TextStyles.Caption1);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, radius * 2f + 14f * scale));
    }

    private void DrawSummaryCard(float scale)
    {
        var player = gameData.LocalPlayer;
        var name = player?.Name.TextValue ?? "Adventurer";
        var world = player is not null ? gameData.WorldName(player.HomeWorld.RowId) : string.Empty;
        ReadIdentity(out var race, out var clan);
        var raceClan = race.Length > 0 && clan.Length > 0 ? $"{race} / {clan}" : race.Length > 0 ? race : "—";

        ui.SectionLabel("Profile summary", TextStyles.FootnoteEmphasized, 4f);
        var card = BeginCard(4, 40f, scale);
        KeyRow(CardRow(card, 0, 40f, scale), "Name", name, scale);
        KeyRow(CardRow(card, 1, 40f, scale), "World", world.Length > 0 ? world : "—", scale);
        KeyRow(CardRow(card, 2, 40f, scale), "Race / Clan", raceClan, scale);
        KeyRow(CardRow(card, 3, 40f, scale), "Height", $"{HealthFormat.Height(tracker.HeightCm, U)} · {tracker.HeightSource}",
            scale);
        EndCard(card, scale);
    }

    private void KeyRow(Rect row, string label, string value, float scale)
    {
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, Pal.MutedInk,
            TextStyles.Subheadline);
        var valueSize = Typography.Measure(value, TextStyles.Headline);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, Pal.TitleInk,
            TextStyles.Headline);
    }

    private void StepLabel(string text, float scale)
    {
        ui.SectionLabel($"Step {setupStep + 1} of {SetupSteps}  ·  {text}", TextStyles.FootnoteEmphasized, 8f);
    }

    private bool RadioRow(string title, string subtitle, bool selected, float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = 48f * scale;
        var min = origin;
        var max = origin + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var fill = selected ? Pal.Accent with { W = 0.18f } : Pal.FieldSurface with { W = Pal.FieldSurface.W * (hovered ? 1.4f : 1f) };
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), 12f * scale);
        if (selected)
        {
            drawList.AddRect(min, max, ImGui.GetColorU32(Pal.Accent), 12f * scale, ImDrawFlags.RoundCornersAll, 1.5f * scale);
        }

        var cy = origin.Y + height * 0.5f;
        var cx = origin.X + 20f * scale;
        var ring = 8f * scale;
        drawList.AddCircle(new Vector2(cx, cy), ring, ImGui.GetColorU32(selected ? Pal.Accent : Pal.MutedInk), 24,
            2f * scale);
        if (selected)
        {
            drawList.AddCircleFilled(new Vector2(cx, cy), ring * 0.5f, ImGui.GetColorU32(Pal.Accent), 16);
        }

        var textLeft = cx + 18f * scale;
        if (subtitle.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, cy - 15f * scale), title, Pal.TitleInk, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, cy + 3f * scale), subtitle, Pal.MutedInk, TextStyles.Footnote);
        }
        else
        {
            var size = Typography.Measure(title, TextStyles.Headline);
            Typography.Draw(new Vector2(textLeft, cy - size.Y * 0.5f), title, Pal.TitleInk, TextStyles.Headline);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 8f * scale));
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    // ---- Hydration ----------------------------------------------------------

    private void DrawHydration(float scale)
    {
        var day = Profile.LatestDay ?? new HealthDay();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        Typography.DrawCentered(new Vector2(origin.X + width * 0.5f, origin.Y + 14f * scale),
            $"{day.DrinkCount} / {Profile.DailyHydrationGoal} drinks today", Pal.TitleInk, TextStyles.Title3);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 34f * scale));

        if (WideButton("Drink Water", true, scale, 46f))
        {
            tracker.LogDrink("Water", 250);
        }

        // Quick drink
        var chipOrigin = ImGui.GetCursorScreenPos();
        var centerY = chipOrigin.Y + 16f * scale;
        var cursorX = chipOrigin.X;
        for (var index = 0; index < DrinkKinds.Length; index++)
        {
            if (ui.FlowChip(ref cursorX, centerY, 8f * scale, DrinkKinds[index], false))
            {
                tracker.LogDrink(DrinkKinds[index], 250);
            }
        }

        ImGui.SetCursorScreenPos(chipOrigin);
        ImGui.Dummy(new Vector2(width, 40f * scale));

        ui.SectionLabel("Custom drink", TextStyles.FootnoteEmphasized, 6f);
        ui.Field("Name", "##health.customName", ref customDrinkName, 24, false);
        var serving = IntField("Serving (ml)", "##hp.water.serving", customDrinkMl, 50, 50, 2000, scale);
        if (serving != customDrinkMl)
        {
            customDrinkMl = serving;
        }

        if (WideButton("Log custom drink", false, scale))
        {
            tracker.LogDrink(customDrinkName.Length > 0 ? customDrinkName : "Drink", customDrinkMl);
        }

        if (WideButton("Undo last drink", false, scale))
        {
            tracker.UndoLastDrink();
        }

        var goalDrinks = IntField("Daily goal (drinks)", "##hp.water.goal", Profile.DailyHydrationGoal, 1, 1, 20, scale);
        if (goalDrinks != Profile.DailyHydrationGoal)
        {
            Profile.DailyHydrationGoal = goalDrinks;
            tracker.SaveNow();
        }

        ui.SectionLabel("Today", TextStyles.FootnoteEmphasized, 6f);
        if (day.Drinks.Count == 0)
        {
            ui.HelpText("No drinks logged yet today.");
        }
        else
        {
            for (var index = day.Drinks.Count - 1; index >= 0; index--)
            {
                var entry = day.Drinks[index];
                var time = DateTimeOffset.FromUnixTimeSeconds(entry.Unix).LocalDateTime.ToString("HH:mm",
                    CultureInfo.InvariantCulture);
                ui.LabelValue($"{time}  {entry.Kind}", HealthFormat.Volume(entry.Millilitres, U));
            }
        }

        DrawReminderSettings(scale);
    }

    private void DrawReminderSettings(float scale)
    {
        ui.SectionLabel("Reminders", TextStyles.FootnoteEmphasized, 8f);
        var enabled = Profile.HydrationRemindersEnabled;
        ui.ToggleRow("Hydration reminders", ref enabled);
        if (enabled != Profile.HydrationRemindersEnabled)
        {
            Profile.HydrationRemindersEnabled = enabled;
            tracker.SaveNow();
        }

        if (!Profile.HydrationRemindersEnabled)
        {
            return;
        }

        var every = IntField("Every (min)", "##hp.remind.every", Profile.ReminderIntervalMinutes, 5, 1, 720, scale);
        if (every != Profile.ReminderIntervalMinutes)
        {
            Profile.ReminderIntervalMinutes = every;
            tracker.SaveNow();
        }

        var (fromHour, fromMinute) = TimeField("Quiet from", "##hp.remind.from", Profile.QuietStartHour,
            Profile.QuietStartMinute, scale);
        if (fromHour != Profile.QuietStartHour || fromMinute != Profile.QuietStartMinute)
        {
            Profile.QuietStartHour = fromHour;
            Profile.QuietStartMinute = fromMinute;
            tracker.SaveNow();
        }

        var (untilHour, untilMinute) = TimeField("Quiet until", "##hp.remind.until", Profile.QuietEndHour,
            Profile.QuietEndMinute, scale);
        if (untilHour != Profile.QuietEndHour || untilMinute != Profile.QuietEndMinute)
        {
            Profile.QuietEndHour = untilHour;
            Profile.QuietEndMinute = untilMinute;
            tracker.SaveNow();
        }

        var pause = Profile.ReminderPauseInDuties;
        ui.ToggleRow("Pause during combat / duties", ref pause);
        if (pause != Profile.ReminderPauseInDuties)
        {
            Profile.ReminderPauseInDuties = pause;
            tracker.SaveNow();
        }
    }

    // ---- Goals --------------------------------------------------------------

    private void DrawGoals(float scale)
    {
        for (var index = 0; index < Profile.Goals.Count; index++)
        {
            var goal = Profile.Goals[index];
            GoalBar(goal, scale);
            if (editingGoalId == goal.Id)
            {
                DrawGoalEditor(goal, scale);
            }
            else if (WideButton(goal.Enabled ? "Edit" : "Edit (disabled)", false, scale, 30f))
            {
                editingGoalId = goal.Id;
                goalNameBuffer = goal.Name;
            }

            ImGui.Dummy(new Vector2(0f, 6f * scale));
        }

        if (WideButton("Add goal", true, scale))
        {
            var goal = new HealthGoal { Name = "New goal", Type = HealthGoalType.Steps, Target = 1000 };
            Profile.Goals.Add(goal);
            editingGoalId = goal.Id;
            goalNameBuffer = goal.Name;
            tracker.SaveNow();
        }

        if (WideButton("Reset to default goals", false, scale))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = "Reset goals",
                Message = "Replace your goals with the defaults?",
                ConfirmLabel = "Reset",
                CancelLabel = "Cancel",
                Confirm = () =>
                {
                    Profile.Goals = HealthTracker.DefaultGoals();
                    editingGoalId = null;
                    tracker.SaveNow();
                },
            });
        }
    }

    private void DrawGoalEditor(HealthGoal goal, float scale)
    {
        ui.Field("Name", "##health.goalName", ref goalNameBuffer, 40, false);

        var typeDelta = StepperRow("Type", GoalTypeLabel(goal.Type), scale);
        if (typeDelta != 0)
        {
            goal.Type = Cycle(goal.Type, typeDelta);
            tracker.SaveNow();
        }

        var scopeDelta = StepperRow("Scope", ScopeLabels[(int)goal.Scope], scale);
        if (scopeDelta != 0)
        {
            goal.Scope = (HealthGoalScope)(((int)goal.Scope + scopeDelta + 4) % 4);
            tracker.SaveNow();
        }

        var target = FloatField("Target", "##hp.goalTarget", goal.Target, GoalStep(goal.Type), 1, 10_000_000,
            "%.0f", scale);
        if (Math.Abs(target - goal.Target) > 0.001)
        {
            goal.Target = target;
            goal.CompletedKey = string.Empty;
            tracker.SaveNow();
        }

        var enabled = goal.Enabled;
        ui.ToggleRow("Enabled", ref enabled);
        if (enabled != goal.Enabled)
        {
            goal.Enabled = enabled;
            tracker.SaveNow();
        }

        if (WideButton("Delete goal", false, scale, 30f))
        {
            Profile.Goals.Remove(goal);
            editingGoalId = null;
            tracker.SaveNow();
            return;
        }

        if (WideButton("Done", true, scale, 30f))
        {
            goal.Name = goalNameBuffer.Length > 0 ? goalNameBuffer : "Goal";
            editingGoalId = null;
            tracker.SaveNow();
        }
    }

    private static string GoalTypeLabel(HealthGoalType type) => type switch
    {
        HealthGoalType.Steps => "Steps",
        HealthGoalType.OnFootDistance => "On-foot distance",
        HealthGoalType.WalkDistance => "Walking distance",
        HealthGoalType.RunDistance => "Running distance",
        HealthGoalType.SwimDistance => "Swimming distance",
        HealthGoalType.ActiveTime => "Active time",
        HealthGoalType.HydrationCount => "Drinks logged",
        HealthGoalType.HydrationVolume => "Drink volume",
        HealthGoalType.Teleports => "Teleports",
        HealthGoalType.TeleportDistance => "Teleport distance",
        _ => "Est. energy",
    };

    private static double GoalStep(HealthGoalType type) => type switch
    {
        HealthGoalType.Steps => 500,
        HealthGoalType.ActiveTime => 300,
        HealthGoalType.HydrationCount or HealthGoalType.Teleports => 1,
        HealthGoalType.HydrationVolume => 250,
        HealthGoalType.Calories => 50,
        _ => 100,
    };

    private static HealthGoalType Cycle(HealthGoalType type, int delta)
    {
        var count = Enum.GetValues<HealthGoalType>().Length;
        return (HealthGoalType)(((int)type + delta + count) % count);
    }

    // ---- History ------------------------------------------------------------

    private void DrawHistory(float scale)
    {
        var days = Profile.Days;
        if (days.Count == 0)
        {
            ui.HelpText("No activity recorded yet.");
            return;
        }

        var shown = 0;
        for (var index = days.Count - 1; index >= 0 && shown < 7; index--, shown++)
        {
            var day = days[index];
            var steps = HealthFormat.Steps(day.OnFootYalms, Profile.StrideYalms);
            ui.SectionLabel($"{FormatDate(day.Date)}  ·  {day.GoalsCompleted} goals · {day.Teleports} tp",
                TextStyles.FootnoteEmphasized, 6f);
            var card = BeginCard(4, CompactRowHeight, scale);
            StatRow(CardRow(card, 0, CompactRowHeight, scale), Accent1, FontAwesomeIcon.Walking,
                $"{HealthFormat.Number(steps)} steps", Dist(day.OnFootYalms), scale);
            StatRow(CardRow(card, 1, CompactRowHeight, scale), Accent2, FontAwesomeIcon.Clock,
                "Active", HealthFormat.Duration(day.ActiveSeconds), scale);
            StatRow(CardRow(card, 2, CompactRowHeight, scale), Accent4, FontAwesomeIcon.Tint,
                "Hydration", $"{day.DrinkCount} drinks", scale);
            var kcal = Profile.CaloriesEnabled ? $"{day.Calories:0} kcal" : "—";
            StatRow(CardRow(card, 3, CompactRowHeight, scale), Accent3, FontAwesomeIcon.Fire, "Energy", kcal, scale);
            EndCard(card, scale);
        }
    }

    private static string FormatDate(string key)
    {
        return DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var parsed)
            ? parsed.ToString("ddd, MMM d", CultureInfo.InvariantCulture)
            : key;
    }

    // ---- Profile / Settings -------------------------------------------------

    private void DrawProfile(float scale)
    {
        DrawSummaryCard(scale);

        BeginPanel("Height", scale);
        InfoRow("Reading", $"{HealthFormat.Height(tracker.HeightCm, U)}  ·  {tracker.HeightSource}", scale);
        if (WideButton("Refresh height", false, scale, 30f))
        {
            tracker.RefreshHeight();
        }

        var autoHeight = PanelToggle("Auto-refresh on change", Profile.AutoRefreshHeight, scale);
        if (autoHeight != Profile.AutoRefreshHeight)
        {
            Profile.AutoRefreshHeight = autoHeight;
            tracker.SaveNow();
        }

        var manualDelta = StepperRow("Manual override (cm)",
            Profile.ManualHeightCm is { } m ? $"{m:0.0}" : "off", scale);
        if (manualDelta != 0)
        {
            var baseCm = Profile.ManualHeightCm ?? (tracker.HeightCm > 0 ? tracker.HeightCm : 170);
            Profile.ManualHeightCm = Math.Clamp(baseCm + manualDelta * 0.5, 50, 260);
            tracker.RefreshHeight();
            tracker.SaveNow();
        }

        if (Profile.ManualHeightCm is not null && WideButton("Clear override", false, scale, 30f))
        {
            Profile.ManualHeightCm = null;
            tracker.RefreshHeight();
            tracker.SaveNow();
        }

        EndPanel(scale);

        BeginPanel("Fictional weight", scale);
        InfoRow("Current", Profile.WeightKg is { } kg ? HealthFormat.Weight(kg, U) : "not set", scale);
        PanelField($"Enter weight ({WeightUnitLabel()})", "##health.weight", ref weightBuffer, 8, scale);
        if (WideButton("Set weight", false, scale, 30f) &&
            double.TryParse(weightBuffer, NumberStyles.Any, CultureInfo.CurrentCulture, out var value) && value > 0)
        {
            Profile.WeightKg = HealthFormat.WeightToKg(value, U);
            tracker.SaveNow();
        }

        if (Profile.WeightKg is not null && WideButton("Clear weight", false, scale, 30f))
        {
            Profile.WeightKg = null;
            weightBuffer = string.Empty;
            tracker.SaveNow();
        }

        DrawWeightSuggestions(scale);

        var calories = PanelToggle("Estimate activity energy", Profile.CaloriesEnabled, scale);
        if (calories != Profile.CaloriesEnabled)
        {
            Profile.CaloriesEnabled = calories;
            tracker.SaveNow();
        }

        PanelHint("Character weight is optional and used only for fictional activity-energy estimates.", scale);
        EndPanel(scale);

        BeginPanel("Units", scale);
        var units = Segmented("health.units", UnitLabels, (int)U, scale);
        if (units != (int)U)
        {
            Profile.Units = (HealthUnits)units;
            weightBuffer = string.Empty;
            tracker.SaveNow();
        }

        EndPanel(scale);

        BeginPanel("Stride length", scale);
        var strideDelta = StepperRow("Yalms per step",
            Profile.StrideYalms.ToString("0.00", CultureInfo.CurrentCulture), scale);
        if (strideDelta != 0)
        {
            Profile.StrideYalms = Math.Clamp(Profile.StrideYalms + strideDelta * 0.05, 0.30, 1.50);
            tracker.SaveNow();
        }

        if (WideButton("Suggest from height", false, scale, 30f))
        {
            Profile.StrideYalms = HealthFormat.SuggestStride(tracker.HeightCm);
            tracker.SaveNow();
        }

        PanelHint("Only walking and running produce steps. Raw distance is stored, so changing stride never loses progress.", scale);
        EndPanel(scale);

        BeginPanel("Tracking status", scale);
        InfoRow("Status", tracker.TrackingStatus, scale);
        EndPanel(scale);

        BeginPanel("Reset", scale);
        if (WideButton("Reset session", false, scale, 30f))
        {
            tracker.ResetSession();
        }

        if (WideButton("Reset today", false, scale, 30f))
        {
            AskReset("Reset today's activity?", tracker.ResetToday);
        }

        if (WideButton("Reset today's hydration", false, scale, 30f))
        {
            AskReset("Clear today's hydration entries?", tracker.ResetTodayHydration);
        }

        if (WideButton("Reset history", false, scale, 30f))
        {
            AskReset("Delete recent activity history?", tracker.ResetHistory);
        }

        if (WideButton("Reset personal records", false, scale, 30f))
        {
            AskReset("Reset personal records?", tracker.ResetRecords);
        }

        if (WideButton("Reset all Health data", false, scale, 30f))
        {
            AskReset("Erase ALL Health data for this character? This cannot be undone.", tracker.ResetAll);
        }

        EndPanel(scale);

        PanelHint("Health tracks fictional activity performed by your FFXIV character. Its steps, calories, hydration, and wellness values are estimates intended for roleplay and statistics.", scale);
    }

    private void AskReset(string message, Action confirmed)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = "Confirm",
            Message = message,
            ConfirmLabel = "Reset",
            CancelLabel = "Cancel",
            Confirm = confirmed,
        });
    }

    private string WeightUnitLabel() => U switch
    {
        HealthUnits.Metric => "kg",
        HealthUnits.Imperial => "lb",
        _ => "ponz",
    };

    // Fictional "normal" weight suggestions derived from the character's height and racial build.
    // These are only tappable hints; weight is never auto-assigned.
    private void DrawWeightSuggestions(float scale)
    {
        var cm = tracker.HeightCm;
        if (cm <= 0)
        {
            return;
        }

        PanelLabel("Suggested (tap to use)", scale);
        foreach (var (label, kg) in WeightSuggestions(cm))
        {
            if (WideButton($"{label}  ·  {HealthFormat.Weight(kg, U)}", false, scale, 30f))
            {
                Profile.WeightKg = kg;
                weightBuffer = string.Empty;
                tracker.SaveNow();
            }
        }

        PanelHint("Fictional estimates from your character's height and build.", scale);
    }

    private void PanelLabel(string text, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(basePos.X + groupPad, basePos.Y), text, Pal.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, 18f * scale));
    }

    private (string Label, double Kg)[] WeightSuggestions(double cm)
    {
        var metres = cm / 100d;
        var build = RaceBuildFactor();
        return new (string, double)[]
        {
            ("Lean", Math.Round(19.5 * metres * metres * build)),
            ("Average", Math.Round(23.0 * metres * metres * build)),
            ("Sturdy", Math.Round(26.5 * metres * metres * build)),
        };
    }

    private double RaceBuildFactor()
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return 1.0;
        }

        try
        {
            var customize = player.Customize;
            if (customize.Length >= 1)
            {
                return customize[0] switch
                {
                    3 => 1.12,  // Lalafell — small but stocky
                    5 => 1.18,  // Roegadyn — heavy, muscular
                    7 => 1.20,  // Hrothgar — huge, muscular
                    2 => 0.95,  // Elezen — slender
                    8 => 0.93,  // Viera — slender
                    _ => 1.0,   // Hyur, Miqo'te, Au Ra
                };
            }
        }
        catch
        {
            // Appearance unreadable; use a neutral build.
        }

        return 1.0;
    }

    private void ReadIdentity(out string race, out string clan)
    {
        race = string.Empty;
        clan = string.Empty;
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return;
        }

        try
        {
            var customize = player.Customize;
            if (customize.Length >= 5)
            {
                var female = customize[1] == 1;
                race = gameData.RaceName(customize[0], female);
                clan = gameData.ClanName(customize[4], female);
            }
        }
        catch
        {
            // Appearance not readable right now; identity stays blank.
        }
    }

    // ---- Small controls -----------------------------------------------------

    private int StepperRow(string label, string value, float scale, float rowHeight = 40f)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var row = new Rect(origin, origin + new Vector2(width, rowHeight * scale));
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - 8f * scale), label, Pal.BodyInk, TextStyles.Subheadline);
        var radius = 13f * scale;
        var plus = new Vector2(row.Max.X - radius, row.Center.Y);
        var minus = new Vector2(row.Max.X - radius - 96f * scale, row.Center.Y);
        var valueCenter = new Vector2((plus.X + minus.X) * 0.5f, row.Center.Y);
        Typography.DrawCentered(valueCenter, value, Pal.TitleInk, 0.95f, FontWeight.SemiBold);
        var delta = 0;
        if (ui.IconButton(minus, radius, FontAwesomeIcon.Minus.ToIconString(), Pal.TitleInk, Pal.FieldSurface, 0.5f))
        {
            delta--;
        }

        if (ui.IconButton(plus, radius, FontAwesomeIcon.Plus.ToIconString(), Pal.TitleInk, Pal.FieldSurface, 0.5f))
        {
            delta++;
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, (rowHeight + 6f) * scale));
        return delta;
    }

    private string? activeNumberId;

    private int IntField(string label, string id, int value, int step, int min, int max, float scale)
    {
        NumberField(label, scale, out var inputPos, out var inputWidth, out var boxCenter, out var dec, out var inc,
            out var basePos, out var full, out var rowHeight);
        var v = Math.Clamp(value + (dec ? -step : 0) + (inc ? step : 0), min, max);
        var active = activeNumberId == id;
        ImGui.SetCursorScreenPos(inputPos);
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, active ? Pal.TitleInk : AppSkin.Transparent))
        {
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt(id, ref v, 0, 0);
        }

        UpdateActiveNumber(id);
        if (activeNumberId != id)
        {
            Typography.DrawCentered(boxCenter, v.ToString(CultureInfo.CurrentCulture), Pal.TitleInk,
                TextStyles.Headline);
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight));
        return Math.Clamp(v, min, max);
    }

    private double FloatField(string label, string id, double value, double step, double min, double max,
        string format, float scale)
    {
        NumberField(label, scale, out var inputPos, out var inputWidth, out var boxCenter, out var dec, out var inc,
            out var basePos, out var full, out var rowHeight);
        var v = (float)Math.Clamp(value + (dec ? -step : 0) + (inc ? step : 0), min, max);
        var active = activeNumberId == id;
        ImGui.SetCursorScreenPos(inputPos);
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, active ? Pal.TitleInk : AppSkin.Transparent))
        {
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputFloat(id, ref v, 0f, 0f, format);
        }

        UpdateActiveNumber(id);
        if (activeNumberId != id)
        {
            var decimals = format.Contains(".2") ? "0.00" : format.Contains(".1") ? "0.0" : "0";
            Typography.DrawCentered(boxCenter, v.ToString(decimals, CultureInfo.CurrentCulture), Pal.TitleInk,
                TextStyles.Headline);
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight));
        return Math.Clamp(v, min, max);
    }

    private void UpdateActiveNumber(string id)
    {
        if (ImGui.IsItemActive())
        {
            activeNumberId = id;
        }
        else if (activeNumberId == id)
        {
            activeNumberId = null;
        }
    }

    private void NumberField(string label, float scale, out Vector2 inputPos, out float inputWidth,
        out Vector2 boxCenter, out bool dec, out bool inc, out Vector2 basePos, out float full, out float rowHeight)
    {
        full = ImGui.GetContentRegionAvail().X;
        basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var frameHeight = ImGui.GetFrameHeight();
        rowHeight = frameHeight + 10f * scale;
        var centerY = basePos.Y + rowHeight * 0.5f;
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(new Vector2(origin.X, centerY - labelSize.Y * 0.5f), label, Pal.BodyInk,
            TextStyles.Subheadline);

        var radius = 13f * scale;
        var gap = 8f * scale;
        inputWidth = 88f * scale;
        var rightEdge = origin.X + width;
        var plusCenter = new Vector2(rightEdge - radius, centerY);
        var inputRight = plusCenter.X - radius - gap;
        var inputLeft = inputRight - inputWidth;
        var minusCenter = new Vector2(inputLeft - gap - radius, centerY);
        dec = ui.IconButton(minusCenter, radius, FontAwesomeIcon.Minus.ToIconString(), Pal.TitleInk,
            Pal.FieldSurface, 0.5f);
        inc = ui.IconButton(plusCenter, radius, FontAwesomeIcon.Plus.ToIconString(), Pal.TitleInk,
            Pal.FieldSurface, 0.5f);

        var boxMin = new Vector2(inputLeft, centerY - frameHeight * 0.5f);
        var boxMax = new Vector2(inputRight, centerY + frameHeight * 0.5f);
        ImGui.GetWindowDrawList().AddRectFilled(boxMin, boxMax, ImGui.GetColorU32(Pal.FieldSurface), 8f * scale);
        boxCenter = new Vector2((inputLeft + inputRight) * 0.5f, centerY);
        inputPos = boxMin;
    }

    // A single centered numeric box (no label, no steppers); used to compose fields like TimeField.
    private int IntBox(string id, float inputLeft, float centerY, float inputWidth, float frameHeight, int value,
        int min, int max, string overlayFormat, float scale)
    {
        var boxMin = new Vector2(inputLeft, centerY - frameHeight * 0.5f);
        var boxMax = new Vector2(inputLeft + inputWidth, centerY + frameHeight * 0.5f);
        ImGui.GetWindowDrawList().AddRectFilled(boxMin, boxMax, ImGui.GetColorU32(Pal.FieldSurface), 8f * scale);
        var v = Math.Clamp(value, min, max);
        var active = activeNumberId == id;
        ImGui.SetCursorScreenPos(boxMin);
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, active ? Pal.TitleInk : AppSkin.Transparent))
        {
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.InputInt(id, ref v, 0, 0);
        }

        UpdateActiveNumber(id);
        if (activeNumberId != id)
        {
            Typography.DrawCentered(new Vector2((boxMin.X + boxMax.X) * 0.5f, centerY),
                v.ToString(overlayFormat, CultureInfo.CurrentCulture), Pal.TitleInk, TextStyles.Headline);
        }

        return Math.Clamp(v, min, max);
    }

    // Label on the left; two centered HH / MM boxes separated by a colon on the right.
    private (int Hour, int Minute) TimeField(string label, string id, int hour, int minute, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var frameHeight = ImGui.GetFrameHeight();
        var rowHeight = frameHeight + 10f * scale;
        var centerY = basePos.Y + rowHeight * 0.5f;
        var labelSize = Typography.Measure(label, TextStyles.Subheadline);
        Typography.Draw(new Vector2(origin.X, centerY - labelSize.Y * 0.5f), label, Pal.BodyInk,
            TextStyles.Subheadline);

        var boxWidth = 54f * scale;
        var colonGap = 16f * scale;
        var rightEdge = origin.X + width;
        var minuteLeft = rightEdge - boxWidth;
        var colonX = minuteLeft - colonGap * 0.5f;
        var hourLeft = minuteLeft - colonGap - boxWidth;
        var h = IntBox(id + ".h", hourLeft, centerY, boxWidth, frameHeight, hour, 0, 23, "00", scale);
        Typography.DrawCentered(new Vector2(colonX, centerY), ":", Pal.TitleInk, TextStyles.Headline);
        var m = IntBox(id + ".m", minuteLeft, centerY, boxWidth, frameHeight, minute, 0, 59, "00", scale);

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight));
        return (h, m);
    }

    private int Segmented(string id, string[] options, int selected, float scale, float rowHeight = 32f)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var rect = new Rect(origin, origin + new Vector2(width, rowHeight * scale));
        var result = SegmentStrip.Draw(id, rect, options, selected, Pal);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, (rowHeight + 6f) * scale));
        return result;
    }

    // ---- Card panels ------

    private Vector2 panelStart;
    private float panelWidth;

    private void BeginPanel(string title, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);
        panelStart = ImGui.GetCursorScreenPos();
        panelWidth = ImGui.GetContentRegionAvail().X;
        ImGui.Dummy(new Vector2(panelWidth, 10f * scale));
        groupPad = 14f * scale;
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(new Vector2(origin.X + groupPad, origin.Y),
            CultureInfo.CurrentCulture.TextInfo.ToUpper(title), Pal.HeaderInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(panelWidth, 22f * scale));
    }

    private void EndPanel(float scale)
    {
        ImGui.Dummy(new Vector2(panelWidth, 10f * scale));
        var end = ImGui.GetCursorScreenPos();
        groupPad = 0f;
        var drawList = ImGui.GetWindowDrawList();
        drawList.ChannelsSetCurrent(0);
        ui.Card(drawList, panelStart, new Vector2(panelStart.X + panelWidth, end.Y), 18f * scale, elevated: true);
        drawList.ChannelsMerge();
        ImGui.Dummy(new Vector2(panelWidth, 12f * scale));
    }

    private void InfoRow(string label, string value, float scale, float rowHeight = 34f)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        KeyRow(new Rect(origin, origin + new Vector2(full - groupPad * 2f, rowHeight * scale)), label, value, scale);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, rowHeight * scale));
    }

    private bool PanelToggle(string label, bool value, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        var height = 34f * scale;
        var row = new Rect(origin, origin + new Vector2(width, height));
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - 8f * scale), label, Pal.BodyInk,
            TextStyles.Subheadline);
        var trackWidth = 44f * scale;
        var trackHeight = 24f * scale;
        var trackMin = new Vector2(row.Max.X - trackWidth, row.Center.Y - trackHeight * 0.5f);
        var trackMax = new Vector2(row.Max.X, row.Center.Y + trackHeight * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(value ? Pal.Accent : Pal.FieldSurface),
            trackHeight * 0.5f);
        var knobRadius = trackHeight * 0.5f - 3f * scale;
        var knobX = value ? trackMax.X - knobRadius - 3f * scale : trackMin.X + knobRadius + 3f * scale;
        drawList.AddCircleFilled(new Vector2(knobX, row.Center.Y), knobRadius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 20);
        var clicked = ImGui.IsMouseHoveringRect(row.Min, row.Max) && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, height + 6f * scale));
        return clicked ? !value : value;
    }

    private void PanelField(string label, string id, ref string value, int maxLength, float scale)
    {
        var full = ImGui.GetContentRegionAvail().X;
        var basePos = ImGui.GetCursorScreenPos();
        var origin = new Vector2(basePos.X + groupPad, basePos.Y);
        var width = full - groupPad * 2f;
        Typography.Draw(origin, label, Pal.MutedInk, TextStyles.Footnote);
        var boxTop = origin.Y + 18f * scale;
        var boxHeight = 32f * scale;
        var min = new Vector2(origin.X, boxTop);
        var max = new Vector2(origin.X + width, boxTop + boxHeight);
        ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(Pal.FieldSurface), 8f * scale);
        ImGui.SetCursorScreenPos(new Vector2(min.X + 10f * scale, boxTop + boxHeight * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(width - 20f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, Pal.TitleInk))
        {
            ImGui.InputText(id, ref value, maxLength, ImGuiInputTextFlags.None);
        }

        ImGui.SetCursorScreenPos(basePos);
        ImGui.Dummy(new Vector2(full, (18f + 32f + 8f) * scale));
    }

    private void PanelHint(string text, float scale)
    {
        if (groupPad > 0f)
        {
            ImGui.Indent(groupPad);
        }

        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, Pal.MutedInk))
        {
            Typography.Wrapped(text);
        }

        ImGui.PopTextWrapPos();
        if (groupPad > 0f)
        {
            ImGui.Unindent(groupPad);
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));
    }
}
