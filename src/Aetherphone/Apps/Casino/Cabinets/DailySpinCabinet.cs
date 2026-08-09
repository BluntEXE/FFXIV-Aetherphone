using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

// The only wheel in the building that mints. A cash-out hands back money the player already owned
// and stays quiet about it; this one creates coins that did not exist a second ago, so it gets the
// confetti, the count up and the loud number.
//
// The rim never replays. A spin claimed on this device turns, decelerates and lands on the segment
// the server named; a spin claimed earlier today (or on another device) is already history, so the
// wheel is simply painted at rest on that segment with no fanfare at all. A wheel that re-enacted
// yesterday's win every time the screen opened would be a slot machine pretending to be a gift.
internal sealed class DailySpinCabinet
{
    private const float MaxRingRadius = 104f;
    private const float PillHeight = 48f;
    private const int SpinTurns = 5;

    private static readonly Vector4 Gold = new(1f, 0.84f, 0.42f, 1f);

    private static readonly Vector4[] ConfettiPalette =
    {
        new(1.00f, 0.84f, 0.42f, 1f),
        new(1.00f, 0.95f, 0.75f, 1f),
        new(0.55f, 0.92f, 0.88f, 1f),
        new(0.80f, 0.58f, 0.98f, 1f),
    };

    private readonly CasinoSpinStore spin;
    private readonly ParticleSystem particles = new(192);

    private RollingValue coinRoll;
    private string inlineReason = string.Empty;
    private string spunRoundId = string.Empty;
    private float angle;
    private float spinFromAngle;
    private float sweep;
    private float spinElapsedSeconds = WheelChoreography.SpinSeconds;
    private int landedSegment = -1;
    private long landedAmount;
    private bool spinning;
    private bool celebrated;
    private Vector2 ringCenter;

    public DailySpinCabinet(CasinoSpinStore spin)
    {
        this.spin = spin;
    }

    public void Enter()
    {
        inlineReason = string.Empty;
        spin.RefreshNow();
    }

    public void Reset()
    {
        particles.Clear();
        inlineReason = string.Empty;
        spunRoundId = string.Empty;
        angle = 0f;
        spinFromAngle = 0f;
        sweep = 0f;
        spinElapsedSeconds = WheelChoreography.SpinSeconds;
        landedSegment = -1;
        landedAmount = 0;
        spinning = false;
        celebrated = false;
        coinRoll.Snap(0);
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        spin.EnsureFresh();
        ConsumeClaimResult();
        Advance(delta, scale);
        particles.Update(delta);

        var drawList = ImGui.GetWindowDrawList();
        var pad = Metrics.Space.Md * scale;
        var left = body.Min.X + pad;
        var width = body.Width - pad * 2f;
        var state = spin.State;
        var claim = DailySpinStatus.Of(spin.Loaded, state);
        if (claim == DailySpinClaim.Unknown)
        {
            LoadingPulse.Draw(body.Center, 16f * scale, ui.Palette.Accent, ui.MutedInk, LoadingPulse.SafeLabel());
            return;
        }

        var y = body.Min.Y + Metrics.Space.Sm * scale;
        var intro = Loc.T(L.Casino.SpinIntro);
        var introBlock = Typography.MeasureWrappedBlock(intro, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(new Vector2(left, y), intro, ui.MutedInk, TextStyles.Footnote, width);
        y += introBlock.Y + Metrics.Space.Md * scale;

        y = DrawRing(drawList, left, y, width, scale);
        y = DrawBanner(drawList, ui, claim, left, y, width, scale, delta);
        DrawAction(drawList, ui, state, claim, left, y, width, scale);
        particles.Draw(drawList, scale);
    }

    private void ConsumeClaimResult()
    {
        if (spin.TakeClaimFailure())
        {
            inlineReason = CasinoReasons.Unreachable;
        }

        var result = spin.TakeClaimResult();
        if (result is null)
        {
            return;
        }

        if (!result.Granted)
        {
            inlineReason = result.Reason.Length > 0 ? result.Reason : CasinoReasons.Claimed;
            return;
        }

        inlineReason = string.Empty;
        spunRoundId = result.RoundId;
        landedSegment = result.Segment;
        landedAmount = result.Amount;
        celebrated = false;
        BeginSpin(result.Segment);
    }

    private void BeginSpin(int segment)
    {
        if (!DailySpinRules.IsSegment(segment))
        {
            spinning = false;
            return;
        }

        spinFromAngle = angle;
        sweep = WheelChoreography.SweepFor(spinFromAngle, segment, DailySpinRules.SegmentCount, SpinTurns);
        spinElapsedSeconds = 0f;
        spinning = true;
        coinRoll.Snap(0);
    }

    // A spin claimed before this screen opened is not staged, it is stated: the rim snaps to the
    // segment the server recorded and nothing celebrates, because the coins landed in the wallet
    // long before this frame.
    private void Advance(float deltaSeconds, float scale)
    {
        if (spinning)
        {
            spinElapsedSeconds += deltaSeconds;
            if (spinElapsedSeconds >= WheelChoreography.SpinSeconds)
            {
                spinElapsedSeconds = WheelChoreography.SpinSeconds;
                spinning = false;
                Celebrate(scale);
            }

            angle = WheelChoreography.AngleAt(spinFromAngle, sweep, spinElapsedSeconds);
            return;
        }

        var state = spin.State;
        if (state is null || !DailySpinRules.IsSegment(state.Segment))
        {
            return;
        }

        if (string.Equals(spunRoundId, state.RoundId, StringComparison.Ordinal) && spunRoundId.Length > 0)
        {
            return;
        }

        landedSegment = state.Segment;
        landedAmount = state.Amount;
        celebrated = true;
        angle = WheelChoreography.RestAngleOf(state.Segment, DailySpinRules.SegmentCount);
        coinRoll.Snap((int)Math.Min(landedAmount, int.MaxValue));
    }

    private void Celebrate(float scale)
    {
        if (celebrated || landedAmount <= 0)
        {
            return;
        }

        celebrated = true;
        if (DailySpinRules.IsTopAward(landedSegment))
        {
            particles.Confetti(ringCenter, 110, ConfettiPalette, 340f * scale, 5f, 1.7f);
            particles.Sparkle(ringCenter, 28, Gold, 200f * scale, 4f, 1.1f);
            return;
        }

        particles.Confetti(ringCenter, 48, ConfettiPalette, 250f * scale, 4f, 1.2f);
    }

    private float DrawRing(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var radius = MathF.Min(width * 0.40f, MaxRingRadius * scale);
        var center = new Vector2(left + width * 0.5f, y + radius + 14f * scale);
        ringCenter = center;
        var glow = !spinning && DailySpinRules.IsSegment(landedSegment)
            ? 0.55f + 0.45f * Pulse.Wave(Pulse.Breath)
            : 0f;
        var highlight = spinning ? -1 : landedSegment;
        SpinRingArt.Draw(drawList, center, radius, angle, highlight, glow, scale);
        WheelRingArt.DrawPointer(drawList, center, radius, scale);
        return center.Y + radius + 22f * scale;
    }

    private float DrawBanner(ImDrawListPtr drawList, AppSkin ui, DailySpinClaim claim, float left, float y,
        float width, float scale, float delta)
    {
        var center = new Vector2(left + width * 0.5f, y + 16f * scale);
        if (spinning)
        {
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.SpinTurning), Gold,
                TextStyles.SubheadlineEmphasized);
            return y + 40f * scale;
        }

        if (claim == DailySpinClaim.Available)
        {
            Typography.DrawCentered(drawList, center,
                Loc.T(L.Casino.SpinTopNote, GameNumber.Label((int)DailySpinRules.TopAward)), ui.MutedInk,
                TextStyles.Footnote);
            return y + 40f * scale;
        }

        if (landedAmount > 0)
        {
            coinRoll.Update((int)Math.Min(landedAmount, int.MaxValue), delta);
            var amount = ((long)coinRoll.Display).ToString("N0", Loc.Culture);
            Typography.DrawCentered(drawList, center, Loc.T(L.Casino.SpinWonBanner, amount), Gold,
                TextStyles.Title3.Scale * coinRoll.PopScale, TextStyles.Title3.Weight);
            return y + 44f * scale;
        }

        Typography.DrawCentered(drawList, center, Loc.T(L.Casino.SpinClaimedTitle), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        return y + 40f * scale;
    }

    private void DrawAction(ImDrawListPtr drawList, AppSkin ui,
        Core.Aethernet.Contracts.CasinoDailySpinStateDto? state, DailySpinClaim claim, float left, float y,
        float width, float scale)
    {
        if (inlineReason.Length > 0)
        {
            y = DrawReasonCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(inlineReason)), left, y, width, scale)
                + Metrics.Space.Sm * scale;
        }
        else if (claim == DailySpinClaim.Denied && state is not null)
        {
            y = DrawReasonCard(drawList, ui, Loc.T(CasinoReasons.MessageFor(state.Reason)), left, y, width, scale)
                + Metrics.Space.Sm * scale;
        }

        if (claim == DailySpinClaim.Available && !spinning)
        {
            var enabled = DailySpinStatus.CanClaim(spin.Loaded, state, spin.Claiming);
            var pillRect = new Rect(new Vector2(left + width * 0.18f, y),
                new Vector2(left + width * 0.82f, y + PillHeight * scale));
            if (AppSkin.PillButton(pillRect, Loc.T(L.Casino.SpinAction), true, enabled, ui.Theme))
            {
                inlineReason = string.Empty;
                spin.Claim();
            }

            return;
        }

        if (spinning)
        {
            return;
        }

        var reset = state is not null && state.NextClaimAtUnix > 0
            ? Loc.T(L.Casino.SpinNextAt, TimeText.FutureMoment(state.NextClaimAtUnix))
            : Loc.T(L.Casino.SpinNextSoon);
        Typography.DrawCentered(drawList, new Vector2(left + width * 0.5f, y + 10f * scale), reset, ui.MutedInk,
            TextStyles.Footnote);
    }

    private static float DrawReasonCard(ImDrawListPtr drawList, AppSkin ui, string message, float left, float y,
        float width, float scale)
    {
        var pad = 12f * scale;
        var block = Typography.MeasureWrappedBlock(message, TextStyles.Footnote, width - pad * 2f);
        var height = block.Y + pad * 2f;
        var min = new Vector2(left, y);
        var max = new Vector2(left + width, y + height);
        Squircle.Fill(drawList, min, max, 16f * scale, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.10f)));
        Squircle.Stroke(drawList, min, max, 16f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 1f * scale);
        Typography.DrawWrappedLeft(new Vector2(min.X + pad, min.Y + pad), message, ui.TitleInk,
            TextStyles.Footnote, width - pad * 2f);
        return max.Y;
    }
}
