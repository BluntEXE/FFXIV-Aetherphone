using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class BarkeepVerbStage
{
    public const float PourFillPerSecond = 0.45f;
    public const float ShakeBeatSeconds = 0.8f;
    public const int ShakeBeatCount = 4;
    public const float ShakeLeadInSeconds = 0.8f;
    public const float ShakeGraceSeconds = 0.5f;
    public const int LayerCount = 3;
    public const float LayerSwingSeconds = 1.6f;
    public const float GarnishSweepSeconds = 1.3f;

    private static readonly Vector4 Gold = BarkeepArt.PerfectGold;

    private readonly int[] tapGrades = new int[ShakeBeatCount];

    private int kind = -1;
    private float seconds;
    private bool active;
    private int pendingGrade = -1;
    private int pendingTapGrade = -1;

    private float pourFill;
    private float pourTarget;
    private bool pourStarted;

    private int shakeTaps;
    private float shakeErrorSum;

    private int layerTaps;
    private float layerErrorSum;

    private float garnishTarget;

    public bool Active => active;

    public int Kind => kind;

    public void Begin(int stepKind, Random random)
    {
        kind = stepKind;
        seconds = 0f;
        active = true;
        pendingGrade = -1;
        pendingTapGrade = -1;
        for (var tapIndex = 0; tapIndex < tapGrades.Length; tapIndex++)
        {
            tapGrades[tapIndex] = -1;
        }

        pourFill = 0f;
        pourStarted = false;
        pourTarget = 0.55f + (float)random.NextDouble() * 0.30f;
        shakeTaps = 0;
        shakeErrorSum = 0f;
        layerTaps = 0;
        layerErrorSum = 0f;
        garnishTarget = 0.30f + (float)random.NextDouble() * 0.40f;
    }

    public void Cancel()
    {
        active = false;
        pendingGrade = -1;
        pendingTapGrade = -1;
    }

    public void Update(float deltaSeconds, bool held, bool tapped)
    {
        if (!active || pendingGrade >= 0)
        {
            return;
        }

        seconds += deltaSeconds;
        switch (kind)
        {
            case BarkeepRules.PourKind:
                UpdatePour(deltaSeconds, held);
                break;
            case BarkeepRules.ShakeKind:
                UpdateShake(tapped);
                break;
            case BarkeepRules.LayerKind:
                UpdateLayer(tapped);
                break;
            default:
                UpdateGarnish(tapped);
                break;
        }
    }

    public bool TryTakeGrade(out int grade)
    {
        if (pendingGrade < 0)
        {
            grade = -1;
            return false;
        }

        grade = pendingGrade;
        pendingGrade = -1;
        active = false;
        return true;
    }

    public bool TryTakeTapGrade(out int grade)
    {
        if (pendingTapGrade < 0)
        {
            grade = -1;
            return false;
        }

        grade = pendingTapGrade;
        pendingTapGrade = -1;
        return true;
    }

    public int TapGradeAt(int tapIndex)
    {
        return tapGrades[tapIndex];
    }

    private void UpdatePour(float deltaSeconds, bool held)
    {
        if (held)
        {
            pourStarted = true;
            pourFill += deltaSeconds * PourFillPerSecond;
            if (pourFill >= 1f)
            {
                pourFill = 1f;
                pendingGrade = BarkeepGrading.GradePour(pourFill, pourTarget);
            }

            return;
        }

        if (pourStarted)
        {
            pendingGrade = BarkeepGrading.GradePour(pourFill, pourTarget);
        }
    }

    private void UpdateShake(bool tapped)
    {
        if (tapped && shakeTaps < ShakeBeatCount)
        {
            var nearestBeat = (int)MathF.Round((seconds - ShakeLeadInSeconds) / ShakeBeatSeconds);
            nearestBeat = Math.Clamp(nearestBeat, 0, ShakeBeatCount - 1);
            var beatTime = ShakeLeadInSeconds + nearestBeat * ShakeBeatSeconds;
            var error = MathF.Min(MathF.Abs(seconds - beatTime), BarkeepGrading.ShakeFullScaleSeconds);
            shakeErrorSum += error;
            tapGrades[shakeTaps] = BarkeepGrading.GradeShake(error);
            shakeTaps++;
            if (shakeTaps == ShakeBeatCount)
            {
                pendingGrade = BarkeepGrading.GradeShake(shakeErrorSum / ShakeBeatCount);
            }
            else
            {
                pendingTapGrade = tapGrades[shakeTaps - 1];
            }

            return;
        }

        var lastBeat = ShakeLeadInSeconds + (ShakeBeatCount - 1) * ShakeBeatSeconds;
        if (seconds <= lastBeat + ShakeGraceSeconds || shakeTaps >= ShakeBeatCount)
        {
            return;
        }

        shakeErrorSum += (ShakeBeatCount - shakeTaps) * BarkeepGrading.ShakeFullScaleSeconds;
        for (var tapIndex = shakeTaps; tapIndex < ShakeBeatCount; tapIndex++)
        {
            tapGrades[tapIndex] = BarkeepGrading.MissGrade;
        }

        shakeTaps = ShakeBeatCount;
        pendingGrade = BarkeepGrading.GradeShake(shakeErrorSum / ShakeBeatCount);
    }

    private void UpdateLayer(bool tapped)
    {
        if (!tapped || layerTaps >= LayerCount)
        {
            return;
        }

        layerErrorSum += MathF.Abs(LayerLevel);
        tapGrades[layerTaps] = BarkeepGrading.GradeLayer(MathF.Abs(LayerLevel));
        layerTaps++;
        if (layerTaps == LayerCount)
        {
            pendingGrade = BarkeepGrading.GradeLayer(layerErrorSum / LayerCount);
        }
        else
        {
            pendingTapGrade = tapGrades[layerTaps - 1];
        }
    }

    private void UpdateGarnish(bool tapped)
    {
        if (!tapped)
        {
            return;
        }

        pendingGrade = BarkeepGrading.GradeGarnish(MathF.Abs(GarnishMarker - garnishTarget));
    }

    public float PourFill => pourFill;

    public float PourTarget => pourTarget;

    public int ShakeTaps => shakeTaps;

    public float ShakeBeatPulse
    {
        get
        {
            var phase = (seconds - ShakeLeadInSeconds) / ShakeBeatSeconds;
            var fraction = phase - MathF.Floor(phase);
            return 1f - fraction;
        }
    }

    public float LayerLevel => MathF.Sin(seconds / LayerSwingSeconds * MathF.PI * 2f);

    public int LayerTaps => layerTaps;

    public float GarnishMarker => 0.5f + 0.5f * MathF.Sin(seconds / GarnishSweepSeconds * MathF.PI * 2f);

    public float GarnishTarget => garnishTarget;

    public void Draw(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        if (!active)
        {
            return;
        }

        switch (kind)
        {
            case BarkeepRules.PourKind:
                DrawPour(drawList, ui, canvas, scale);
                break;
            case BarkeepRules.ShakeKind:
                DrawShake(drawList, ui, canvas, scale);
                break;
            case BarkeepRules.LayerKind:
                DrawLayer(drawList, ui, canvas, scale);
                break;
            default:
                DrawGarnish(drawList, ui, canvas, scale);
                break;
        }
    }

    private void DrawPour(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var glassWidth = MathF.Min(canvas.Width * 0.3f, 84f * scale);
        var glassTop = canvas.Min.Y + canvas.Height * 0.12f;
        var glassBottom = canvas.Max.Y - canvas.Height * 0.12f;
        var glassLeft = canvas.Center.X - glassWidth * 0.5f;
        var glassRight = canvas.Center.X + glassWidth * 0.5f;
        var glassHeight = glassBottom - glassTop;
        var rounding = 8f * scale;
        Squircle.Fill(drawList, new Vector2(glassLeft, glassTop), new Vector2(glassRight, glassBottom), rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, 0.85f)));

        var goodHalf = BarkeepGrading.GoodWithin * BarkeepGrading.PourFullScale;
        var perfectHalf = BarkeepGrading.PerfectWithin * BarkeepGrading.PourFullScale;
        var bandTop = glassBottom - (pourTarget + goodHalf) * glassHeight;
        var bandBottom = glassBottom - (pourTarget - goodHalf) * glassHeight;
        drawList.AddRectFilled(new Vector2(glassLeft, bandTop), new Vector2(glassRight, bandBottom),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.18f)));
        var perfectTop = glassBottom - (pourTarget + perfectHalf) * glassHeight;
        var perfectBottom = glassBottom - (pourTarget - perfectHalf) * glassHeight;
        drawList.AddRectFilled(new Vector2(glassLeft, perfectTop), new Vector2(glassRight, perfectBottom),
            ImGui.GetColorU32(Gold with { W = 0.28f }));

        var inPerfectBand = MathF.Abs(pourFill - pourTarget) <= perfectHalf;
        var overGoodBand = pourFill > pourTarget + goodHalf;
        if (pourFill > 0f)
        {
            var fillTop = glassBottom - pourFill * glassHeight;
            var fillTint = overGoodBand
                ? Palette.WithAlpha(BarkeepArt.MissRed, 0.75f)
                : inPerfectBand
                    ? Gold with { W = 0.8f }
                    : Palette.WithAlpha(ui.Accent, 0.75f);
            drawList.AddRectFilled(new Vector2(glassLeft + 2f * scale, fillTop),
                new Vector2(glassRight - 2f * scale, glassBottom - 2f * scale),
                ImGui.GetColorU32(fillTint), rounding * 0.6f);
        }

        var glassEdge = inPerfectBand
            ? Gold with { W = 0.85f }
            : Palette.WithAlpha(ui.TitleInk, 0.35f);
        Squircle.Stroke(drawList, new Vector2(glassLeft, glassTop), new Vector2(glassRight, glassBottom), rounding,
            ImGui.GetColorU32(glassEdge), MathF.Max(1f, (inPerfectBand ? 2f : 1.4f) * scale));
    }

    private void DrawShake(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var center = canvas.Center;
        var targetRadius = MathF.Min(canvas.Height, canvas.Width) * 0.18f;
        var running = seconds >= ShakeLeadInSeconds - ShakeBeatSeconds && shakeTaps < ShakeBeatCount;
        var phase = (seconds - ShakeLeadInSeconds) / ShakeBeatSeconds;
        var fraction = phase - MathF.Floor(phase);
        var toBeatSeconds = MathF.Min(fraction, 1f - fraction) * ShakeBeatSeconds;
        var inPerfectWindow = running &&
            toBeatSeconds <= BarkeepGrading.PerfectWithin * BarkeepGrading.ShakeFullScaleSeconds;
        var targetTint = inPerfectWindow ? Gold with { W = 0.9f } : Palette.WithAlpha(ui.TitleInk, 0.45f);
        drawList.AddCircle(center, targetRadius, ImGui.GetColorU32(targetTint), 40,
            MathF.Max(1f, (inPerfectWindow ? 2.6f : 2f) * scale));
        if (running)
        {
            var pulseRadius = targetRadius + targetRadius * 1.6f * ShakeBeatPulse;
            var pulseTint = inPerfectWindow ? Gold with { W = 0.85f } : Palette.WithAlpha(ui.Accent, 0.6f);
            drawList.AddCircle(center, pulseRadius, ImGui.GetColorU32(pulseTint), 48,
                MathF.Max(1f, 2.4f * scale));
        }

        BarkeepArt.DrawVerbGlyph(drawList, BarkeepRules.ShakeKind, center, targetRadius * 0.55f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.8f)));

        var pipY = canvas.Max.Y - 14f * scale;
        for (var beatIndex = 0; beatIndex < ShakeBeatCount; beatIndex++)
        {
            var pipX = center.X + (beatIndex - (ShakeBeatCount - 1) * 0.5f) * 18f * scale;
            var done = beatIndex < shakeTaps;
            var pipTint = done
                ? BarkeepArt.GradeTint(tapGrades[beatIndex], ui.Accent)
                : Palette.WithAlpha(ui.MutedInk, 0.4f);
            drawList.AddCircleFilled(new Vector2(pipX, pipY), 4f * scale, ImGui.GetColorU32(pipTint), 12);
        }
    }

    private void DrawLayer(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var glassWidth = MathF.Min(canvas.Width * 0.42f, 120f * scale);
        var glassTop = canvas.Min.Y + canvas.Height * 0.15f;
        var glassBottom = canvas.Max.Y - canvas.Height * 0.15f;
        var glassLeft = canvas.Center.X - glassWidth * 0.5f;
        var glassRight = canvas.Center.X + glassWidth * 0.5f;
        var glassHeight = glassBottom - glassTop;
        Squircle.Fill(drawList, new Vector2(glassLeft, glassTop), new Vector2(glassRight, glassBottom), 8f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Palette.FieldSurface, 0.85f)));

        var layerHeight = glassHeight / (LayerCount + 1);
        for (var layerIndex = 0; layerIndex < layerTaps; layerIndex++)
        {
            var top = glassBottom - (layerIndex + 1) * layerHeight;
            var layerTint = BarkeepArt.GradeTint(tapGrades[layerIndex], ui.Accent);
            drawList.AddRectFilled(new Vector2(glassLeft + 2f * scale, top),
                new Vector2(glassRight - 2f * scale, top + layerHeight - 2f * scale),
                ImGui.GetColorU32(Palette.WithAlpha(layerTint, 0.35f + layerIndex * 0.15f)), 4f * scale);
        }

        var settleY = glassBottom - (layerTaps + 1) * layerHeight;
        drawList.AddLine(new Vector2(glassLeft - 10f * scale, settleY), new Vector2(glassRight + 10f * scale, settleY),
            ImGui.GetColorU32(Gold with { W = 0.5f }), MathF.Max(1f, 1.6f * scale));
        var level = MathF.Abs(LayerLevel);
        var levelPerfect = level <= BarkeepGrading.PerfectWithin * BarkeepGrading.LayerFullScale;
        var swingY = settleY - LayerLevel * layerHeight * 0.9f;
        var swingTint = levelPerfect ? Gold with { W = 0.95f } : Palette.WithAlpha(ui.Accent, 0.9f);
        drawList.AddLine(new Vector2(glassLeft + 2f * scale, swingY), new Vector2(glassRight - 2f * scale, swingY),
            ImGui.GetColorU32(swingTint), MathF.Max(1f, (levelPerfect ? 3f : 2.4f) * scale));
        Squircle.Stroke(drawList, new Vector2(glassLeft, glassTop), new Vector2(glassRight, glassBottom), 8f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.35f)), MathF.Max(1f, 1.4f * scale));
    }

    private void DrawGarnish(ImDrawListPtr drawList, AppSkin ui, Rect canvas, float scale)
    {
        var barY = canvas.Center.Y;
        var barLeft = canvas.Min.X + canvas.Width * 0.12f;
        var barRight = canvas.Max.X - canvas.Width * 0.12f;
        var barWidth = barRight - barLeft;
        drawList.AddLine(new Vector2(barLeft, barY), new Vector2(barRight, barY),
            ImGui.GetColorU32(Palette.WithAlpha(ui.MutedInk, 0.5f)), MathF.Max(1f, 3f * scale));

        var goodHalf = BarkeepGrading.GoodWithin * BarkeepGrading.GarnishFullScale;
        var perfectHalf = BarkeepGrading.PerfectWithin * BarkeepGrading.GarnishFullScale;
        var targetX = barLeft + garnishTarget * barWidth;
        drawList.AddRectFilled(new Vector2(targetX - goodHalf * barWidth, barY - 8f * scale),
            new Vector2(targetX + goodHalf * barWidth, barY + 8f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.20f)), 4f * scale);
        drawList.AddRectFilled(new Vector2(targetX - perfectHalf * barWidth, barY - 10f * scale),
            new Vector2(targetX + perfectHalf * barWidth, barY + 10f * scale),
            ImGui.GetColorU32(Gold with { W = 0.32f }), 4f * scale);

        var markerX = barLeft + GarnishMarker * barWidth;
        var markerPerfect = MathF.Abs(GarnishMarker - garnishTarget) <= perfectHalf;
        var markerTint = markerPerfect ? Gold : ui.Accent;
        drawList.AddCircleFilled(new Vector2(markerX, barY), (markerPerfect ? 9.5f : 8f) * scale,
            ImGui.GetColorU32(markerTint), 20);
        if (markerPerfect)
        {
            drawList.AddCircle(new Vector2(markerX, barY), 13f * scale, ImGui.GetColorU32(Gold with { W = 0.6f }),
                24, MathF.Max(1f, 1.8f * scale));
        }

        BarkeepArt.DrawVerbGlyph(drawList, BarkeepRules.GarnishKind,
            new Vector2(markerX, barY - 24f * scale), 9f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.7f)));
    }
}
