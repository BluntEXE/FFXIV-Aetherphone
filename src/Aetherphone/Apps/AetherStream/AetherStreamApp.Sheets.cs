using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private const float SheetHeightFraction = 0.72f;
    private const float PartyButtonHeight = 44f;
    private const float PartyRowHeight = 48f;

    private void DrawUpNextSheet(Rect area, float scale)
    {
        if (!upNextSheet.IsOpen)
        {
            suggestionNoticesCleared = false;
        }

        upNextSheet.Draw(area, accentedTheme, Loc.T(L.AetherStream.UpNext), SheetHeightFraction,
            content => DrawUpNextContent(content, scale));
    }

    private void DrawScreenSheet(Rect area, float scale)
    {
        screenSheet.Draw(area, accentedTheme, Loc.T(L.AetherStream.Screen), SheetHeightFraction,
            content => DrawScreenContent(content, scale));
    }

    private void DrawPartySheet(Rect area, float scale)
    {
        partySheet.Draw(area, accentedTheme, Loc.T(L.AetherStream.WatchPartyHeader), SheetHeightFraction,
            content => DrawPartyContent(content, scale));
    }

    private void DrawPartyContent(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();

            if (watchAlong.IsAwaitingApproval)
            {
                DrawPartyAwaiting(width, scale);
            }
            else if (watchAlong.IsViewing)
            {
                DrawPartyViewing(width, scale);
            }
            else if (watchAlong.IsHosting || watchAlong.IsPartyOpen)
            {
                DrawPartyHosting(width, scale);
            }
            else
            {
                DrawPartyIdle(width, scale, body);
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private void DrawPartyIdle(float width, float scale, Rect body)
    {
        var origin = ImGui.GetCursorScreenPos();
        var reserved = PartyButtonHeight * scale * 2f + (Metrics.Space.Sm + Metrics.Space.Lg) * scale;
        var emptyHeight = MathF.Max(150f * scale, body.Max.Y - origin.Y - reserved);
        DrawPartyIdleHero(origin, width, emptyHeight, scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, emptyHeight));

        var buttonHeight = PartyButtonHeight * scale;
        var startOrigin = ImGui.GetCursorScreenPos();
        var startRect = new Rect(startOrigin, startOrigin + new Vector2(width, buttonHeight));
        if (ui.PillButton(startRect, Loc.T(L.AetherStream.StartParty), true, "aetherstream.party.start"))
        {
            watchAlong.OpenParty();
        }

        ImGui.SetCursorScreenPos(startOrigin);
        ImGui.Dummy(new Vector2(width, buttonHeight));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));

        var joinOrigin = ImGui.GetCursorScreenPos();
        var joinRect = new Rect(joinOrigin, joinOrigin + new Vector2(width, buttonHeight));
        if (ui.GhostButton(joinRect, Loc.T(L.AetherStream.JoinStream)))
        {
            nearbyRefreshTimer = NearbyRefreshIntervalSeconds;
            partySheet.Close();
            router.Push(AetherStreamScreen.Join);
        }

        ImGui.SetCursorScreenPos(joinOrigin);
        ImGui.Dummy(new Vector2(width, buttonHeight));
    }

    private void DrawPartyIdleHero(Vector2 origin, float width, float height, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var centerX = origin.X + width * 0.5f;
        var iconCenter = new Vector2(centerX, origin.Y + height * 0.5f - 30f * scale);
        drawList.AddCircleFilled(iconCenter, 34f * scale, ImGui.GetColorU32(ui.FieldSurface), 32);
        AppSkin.Icon(iconCenter, IconGlyph.Of(FontAwesomeIcon.UserFriends), ui.MutedInk, 1.7f);
        var maxWidth = MathF.Min(width - 24f * scale, 300f * scale);
        Typography.DrawWrappedCentered(new Vector2(centerX, iconCenter.Y + 52f * scale),
            Loc.T(L.AetherStream.WatchPartyHint), ui.MutedInk, TextStyles.Subheadline, maxWidth);
    }

    private void DrawPartyAwaiting(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var captionHeight = 40f * scale;
        LoadingPulse.Caption(new Vector2(origin.X + width * 0.5f, origin.Y + captionHeight * 0.5f), ui.MutedInk,
            ui.Accent, Loc.T(L.AetherStream.JoinWaitingApproval), 1f, TextStyles.Subheadline.Scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, captionHeight));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));

        var buttonOrigin = ImGui.GetCursorScreenPos();
        var cancelRect = new Rect(buttonOrigin, buttonOrigin + new Vector2(width, PartyButtonHeight * scale));
        if (ui.DangerGhostButton(cancelRect, Loc.T(L.AetherStream.CancelRequest)))
        {
            watchAlong.Leave();
        }

        ImGui.SetCursorScreenPos(buttonOrigin);
        ImGui.Dummy(new Vector2(width, PartyButtonHeight * scale));
    }

    private void DrawPartyViewing(float width, float scale)
    {
        var host = FindHost();
        if (host is not null)
        {
            var origin = ImGui.GetCursorScreenPos();
            var line = string.Format(Loc.T(L.AetherStream.ViewingStream), host.DisplayName);
            var lineHeight = Typography.DrawWrappedLeft(origin, line, ui.MutedInk, TextStyles.Subheadline, width);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, lineHeight + Metrics.Space.Md * scale));
        }

        DrawPartyRoster(width, scale, canKick: false);

        var buttonOrigin = ImGui.GetCursorScreenPos();
        var buttonHeight = PartyButtonHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var half = (width - gap) * 0.5f;
        var resyncRect = new Rect(buttonOrigin, buttonOrigin + new Vector2(half, buttonHeight));
        var leaveRect = new Rect(new Vector2(buttonOrigin.X + width - half, buttonOrigin.Y),
            new Vector2(buttonOrigin.X + width, buttonOrigin.Y + buttonHeight));
        if (ui.GhostButton(resyncRect, Loc.T(L.AetherStream.Resync)))
        {
            watchAlong.ResyncNow();
        }

        if (ui.DangerGhostButton(leaveRect, Loc.T(L.AetherStream.LeaveStream)))
        {
            watchAlong.Leave();
            partySheet.Close();
        }

        ImGui.SetCursorScreenPos(buttonOrigin);
        ImGui.Dummy(new Vector2(width, buttonHeight));
    }

    private void DrawPartyHosting(float width, float scale)
    {
        if (watchAlong.PendingRequests.Count > 0)
        {
            DrawPendingRequests(width, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        }

        DrawPartyRoster(width, scale, canKick: true);

        var buttonOrigin = ImGui.GetCursorScreenPos();
        var endRect = new Rect(buttonOrigin, buttonOrigin + new Vector2(width, PartyButtonHeight * scale));
        if (ui.DangerGhostButton(endRect, Loc.T(L.AetherStream.EndParty)))
        {
            watchAlong.Leave();
            partySheet.Close();
        }

        ImGui.SetCursorScreenPos(buttonOrigin);
        ImGui.Dummy(new Vector2(width, PartyButtonHeight * scale));
    }

    private WatchAlongParticipant? FindHost()
    {
        var roster = watchAlong.Roster;
        for (var index = 0; index < roster.Count; index++)
        {
            if (roster[index].IsHost)
            {
                return roster[index];
            }
        }

        return null;
    }

    private void DrawPartyRoster(float width, float scale, bool canKick)
    {
        var watchers = watchAlong.Watching();
        if (watchers.Count == 0)
        {
            return;
        }

        DrawRosterLabel(Loc.T(L.AetherStream.WatchingHostLabel), width, scale);
        DrawWatcherRow(width, scale, watchers[0], kickable: false);

        if (watchers.Count > 1)
        {
            DrawRosterLabel(Loc.T(L.AetherStream.WatchingSectionLabel), width, scale);
            for (var index = 1; index < watchers.Count; index++)
            {
                DrawWatcherRow(width, scale, watchers[index], canKick);
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
    }

    private void DrawRosterLabel(string label, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), origin, Loc.Culture.TextInfo.ToUpper(label), ui.Palette.HeaderInk,
            TextStyles.FootnoteEmphasized);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width,
            Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xs * scale));
    }

    private void DrawWatcherRow(float width, float scale, WatchAlongParticipant participant, bool kickable)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, origin + new Vector2(width, PartyRowHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        var avatarRadius = 16f * scale;
        var avatarCenter = new Vector2(row.Min.X + avatarRadius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, participant.DisplayName, string.Empty,
            participant.AvatarUrl, remoteImages, lodestone, 0.7f, 20);

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textRight = row.Max.X;
        if (kickable)
        {
            var kickCenter = new Vector2(row.Max.X - 14f * scale, row.Center.Y);
            if (ui.IconButton(kickCenter, 12f * scale, IconGlyph.Of(FontAwesomeIcon.Times), ui.MutedInk,
                    AppSkin.Transparent, 0.55f, Loc.T(L.AetherStream.WatchingKick)))
            {
                watchAlong.KickParticipant(participant.UserId);
            }

            textRight = kickCenter.X - 20f * scale;
        }

        var nameHeight = Typography.LineHeight(TextStyles.Body);
        Marquee.DrawLeft(drawList, new MarqueeId("aetherstream.watcher.", participant.UserId), participant.DisplayName, textLeft,
            row.Center.Y - nameHeight * 0.5f, textRight - textLeft, TextStyles.Body, ui.TitleInk,
            UiInteract.Hover(row.Min, row.Max));

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, PartyRowHeight * scale));
    }
}
