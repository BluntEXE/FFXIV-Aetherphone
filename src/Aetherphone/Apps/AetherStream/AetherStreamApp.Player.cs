using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private static readonly Vector4 WhiteInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HeroPillBacking = new(0f, 0f, 0f, 0.45f);

    private const float HeroAspect = 9f / 16f;
    private const float PartyButtonHeight = 40f;
    private const float PartyRowHeight = 44f;

    private string urlInput = string.Empty;
    private bool queueOnAdd;
    private float composerModeAnimation;
    private string? pendingLocalFile;

    private void DrawPlayerTab(Rect body, float scale)
    {
        if (Interlocked.Exchange(ref pendingLocalFile, null) is { } localPath)
        {
            SubmitLocalFile(localPath);
        }

        using (AppSurface.Begin(body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawHero(width, scale);
            DrawPlaybackError(width, scale);
            DrawNowPlayingTitle(width, scale);
            DrawProgressBlock(width, scale);
            DrawTransportBlock(width, scale);
            DrawVolumeBlock(width, scale);
            DrawComposer(width, scale);
            DrawWatchPartySection(width, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        }
    }

    private VideoQueueEntry? CurrentEntry => watchAlong.IsViewing ? watchAlong.ViewingEntry : queue.Current;

    private void DrawHero(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = width * HeroAspect;
        var min = origin;
        var max = origin + new Vector2(width, height);
        var rounding = Metrics.Radius.Card * scale;
        var drawList = ImGui.GetWindowDrawList();
        var current = CurrentEntry;

        Elevation.Card(drawList, min, max, rounding, scale);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(ui.FieldSurface));

        var thumbnail = VideoThumbnailResolver.Get(remoteImages, http, current?.Url, current?.ThumbnailUrl);
        if (thumbnail is not null)
        {
            drawList.AddImageRounded(thumbnail.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else if (current is not null)
        {
            AppSkin.Icon((min + max) * 0.5f, FontAwesomeIcon.Tv.ToIconString(), ui.MutedInk, 1.8f);
        }
        else
        {
            EmptyState.Draw(new Rect(min, max), ui, FontAwesomeIcon.Tv, Loc.T(L.AetherStream.NothingPlaying),
                Loc.T(L.AetherStream.NothingPlayingHint));
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(ui.Palette.CardStroke), 1f);

        if (screen.Engine.IsActive)
        {
            var pillLabel = current is not null
                ? Loc.T(L.Common.Live)
                : Loc.T(L.AetherStream.PlayerCastingWaiting);
            var pillOrigin = min + new Vector2(Metrics.Space.Md * scale, Metrics.Space.Md * scale);
            var pillSize = new Vector2(LivePill.Width(pillLabel, scale), LivePill.Height(scale));
            Squircle.Fill(drawList, pillOrigin, pillOrigin + pillSize, pillSize.Y * 0.34f,
                ImGui.GetColorU32(HeroPillBacking));
            LivePill.Draw(drawList, pillOrigin, pillLabel, current is not null ? theme.Danger : ui.MutedInk,
                (float)ImGui.GetTime(), scale);

            var canStop = !watchAlong.IsViewing &&
                (queue.Current is not null || video.State != VideoPlaybackState.Idle);
            if (canStop)
            {
                var stopRadius = 13f * scale;
                var stopCenter = new Vector2(max.X - Metrics.Space.Md * scale - stopRadius,
                    min.Y + Metrics.Space.Md * scale + stopRadius);
                if (HoverButton.Circle(drawList, "aetherstream.hero.stop", stopCenter, stopRadius,
                        FontAwesomeIcon.Stop, HeroPillBacking, WhiteInk, ImGui.GetIO().DeltaTime, 1f, true,
                        Loc.T(L.AetherStream.Stop)))
                {
                    queue.Clear();
                }
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawPlaybackError(float width, float scale)
    {
        if (video.LastError is not { } error)
        {
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var pad = Metrics.Space.Md * scale;
        var textWidth = width - pad * 2f;
        var textHeight = Typography.MeasureWrappedBlock(error, TextStyles.Footnote, textWidth).Y;
        var cardHeight = textHeight + pad * 2f;
        var drawList = ImGui.GetWindowDrawList();
        var max = origin + new Vector2(width, cardHeight);
        Squircle.Fill(drawList, origin, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.10f)));
        Squircle.Stroke(drawList, origin, max, Metrics.Radius.Md * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Danger, 0.35f)), 1f);
        Typography.DrawWrappedLeft(origin + new Vector2(pad, pad), error, theme.Danger, TextStyles.Footnote,
            textWidth);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight));
    }

    private void DrawNowPlayingTitle(float width, float scale)
    {
        var current = CurrentEntry;
        if (current is null)
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var origin = ImGui.GetCursorScreenPos();
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        Marquee.DrawLeftAuto("aetherstream.nowPlaying.title", current.Title, origin.X, origin.Y, width,
            TextStyles.Title3, ui.TitleInk);
        var sourceY = origin.Y + titleHeight + 2f * scale;
        var sourceHeight = 0f;
        if (current.Source.Length > 0)
        {
            sourceHeight = Typography.LineHeight(TextStyles.Caption1) + 2f * scale;
            Marquee.DrawLeftAuto("aetherstream.nowPlaying.source", current.Source, origin.X, sourceY, width,
                TextStyles.Caption1, ui.MutedInk);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, titleHeight + sourceHeight + Metrics.Space.Sm * scale));
    }

    private void DrawProgressBlock(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var interactive = !watchAlong.IsViewing;
        var progress = video.Progress;
        var position = progress.Position;
        var duration = progress.Duration;
        var normalized = duration > 0f ? Math.Clamp(position / duration, 0f, 1f) : 0f;
        var sliderRow = new Rect(origin, origin + new Vector2(width, 24f * scale));
        var shown = position;

        if (interactive && duration > 0f)
        {
            var result = Slider.Draw("aetherstream.progress", sliderRow, normalized, accentedTheme, 0f, 0f);
            if (result.Released)
            {
                video.Seek(result.Value * duration);
            }

            if (result.Dragging || result.Released)
            {
                shown = result.Value * duration;
            }
        }
        else
        {
            var track = new Rect(new Vector2(origin.X, sliderRow.Center.Y - 2f * scale),
                new Vector2(origin.X + width, sliderRow.Center.Y + 2f * scale));
            Scrubber.Draw(track, normalized, ui.Accent, Palette.WithAlpha(ui.MutedInk, 0.3f),
                interactive ? 1f : 0.4f);
        }

        var labelY = sliderRow.Max.Y + 2f * scale;
        Typography.Draw(new Vector2(origin.X, labelY), TimeText.MinutesSeconds((int)shown), ui.MutedInk,
            TextStyles.Caption1);
        var remainingText = $"-{TimeText.MinutesSeconds((int)MathF.Max(0f, duration - shown))}";
        var remainingSize = Typography.Measure(remainingText, TextStyles.Caption1);
        Typography.Draw(new Vector2(origin.X + width - remainingSize.X, labelY), remainingText, ui.MutedInk,
            TextStyles.Caption1);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 24f * scale + Typography.LineHeight(TextStyles.Caption1) + 4f * scale));
    }

    private void DrawTransportBlock(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = 60f * scale;
        var centerY = origin.Y + rowHeight * 0.5f;
        var centerX = origin.X + width * 0.5f;
        var interactive = !watchAlong.IsViewing;
        var transportAlpha = interactive ? 1f : 0.4f;
        var progress = video.Progress;
        var position = progress.Position;
        var paused = progress.Paused;

        if (interactive && ui.IconButton(new Vector2(centerX - 132f * scale, centerY), 16f * scale,
                FontAwesomeIcon.UndoAlt.ToIconString(), ui.TitleInk, AppSkin.Transparent, 0.6f))
        {
            video.Seek(Math.Max(0f, position - 10f));
        }

        if (TransportButton.Draw(new Vector2(centerX - 72f * scale, centerY), 18f * scale, TransportAction.Previous,
                ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), transportAlpha, interactive) && interactive)
        {
            queue.Advance();
        }

        var playAction = paused ? TransportAction.Play : TransportAction.Pause;
        var centerRadius = 24f * scale;
        var centerPoint = new Vector2(centerX, centerY);
        ImGui.GetWindowDrawList().AddCircleFilled(centerPoint, centerRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, transportAlpha)), 40);
        if (TransportButton.Draw(centerPoint, centerRadius, playAction, ui.Accent, WhiteInk, transportAlpha,
                interactive) && interactive)
        {
            video.Pause(!paused);
        }

        if (TransportButton.Draw(new Vector2(centerX + 72f * scale, centerY), 18f * scale, TransportAction.Next,
                ui.TitleInk, Palette.WithAlpha(ui.TitleInk, 0.85f), transportAlpha, interactive) && interactive)
        {
            queue.Advance();
        }

        if (interactive && ui.IconButton(new Vector2(centerX + 132f * scale, centerY), 16f * scale,
                FontAwesomeIcon.RedoAlt.ToIconString(), ui.TitleInk, AppSkin.Transparent, 0.6f))
        {
            video.Seek(position + 10f);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));
    }

    private void DrawVolumeBlock(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, origin + new Vector2(width, 26f * scale));
        var result = VolumeSlider.Draw("aetherstream.volume", row, configuration.VideoVolume, accentedTheme);
        if (result.Dragging && Math.Abs(result.Value - configuration.VideoVolume) > 0.001f)
        {
            configuration.VideoVolume = result.Value;
            video.SetVolume((int)(result.Value * 100f));
        }

        if (result.Released)
        {
            configuration.VideoVolume = result.Value;
            video.SetVolume((int)(result.Value * 100f));
            configuration.Save();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, 26f * scale));
    }

    private void DrawComposer(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        var origin = ImGui.GetCursorScreenPos();
        var suggesting = watchAlong.IsViewing;
        var sendRadius = 17f * scale;
        var fieldRowHeight = 44f * scale;
        var fieldRect = new Rect(origin,
            origin + new Vector2(width - sendRadius * 2f - Metrics.Space.Md * scale, fieldRowHeight));
        var hint = suggesting ? Loc.T(L.AetherStream.SuggestHint) : Loc.T(L.AetherStream.UrlHint);
        var submitted = SubmitField.Draw(fieldRect, "##aetherstreamUrl", hint, ref urlInput, accentedTheme, 2000,
            FontAwesomeIcon.Link);

        var canSubmit = urlInput.Trim().Length > 0;
        var sendCenter = new Vector2(origin.X + width - sendRadius, origin.Y + fieldRowHeight * 0.5f);
        var sendIcon = suggesting ? FontAwesomeIcon.PaperPlane : FontAwesomeIcon.ArrowUp;
        var sendBackground = canSubmit ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.35f);
        var sendInk = canSubmit ? WhiteInk : Palette.WithAlpha(WhiteInk, 0.6f);
        if (ui.IconButton(sendCenter, sendRadius, sendIcon.ToIconString(), sendInk, sendBackground, 0.6f) &&
            canSubmit)
        {
            submitted = true;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, fieldRowHeight));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));

        var rowOrigin = ImGui.GetCursorScreenPos();
        var rowHeight = 34f * scale;
        var circleRadius = rowHeight * 0.5f;
        var pasteCenter = new Vector2(rowOrigin.X + circleRadius, rowOrigin.Y + circleRadius);
        if (ui.IconButton(pasteCenter, circleRadius, FontAwesomeIcon.Paste.ToIconString(), ui.TitleInk,
                ui.FieldSurface, 0.5f))
        {
            var clipboard = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboard))
            {
                urlInput = clipboard.Trim();
            }
        }

        if (!suggesting)
        {
            var folderCenter = new Vector2(pasteCenter.X + circleRadius * 2f + Metrics.Space.Sm * scale,
                rowOrigin.Y + circleRadius);
            if (ui.IconButton(folderCenter, circleRadius, FontAwesomeIcon.FolderOpen.ToIconString(), ui.TitleInk,
                    ui.FieldSurface, 0.5f, Loc.T(L.AetherStream.BrowseLocalFile)))
            {
                FilePicker.PickVideo(Loc.T(L.AetherStream.BrowseLocalFile),
                    path => Interlocked.Exchange(ref pendingLocalFile, path));
            }

            var leftEdge = folderCenter.X + circleRadius + Metrics.Space.Md * scale;
            var segmentWidth = MathF.Min(210f * scale, rowOrigin.X + width - leftEdge);
            var segmentRect = new Rect(new Vector2(rowOrigin.X + width - segmentWidth, rowOrigin.Y),
                new Vector2(rowOrigin.X + width, rowOrigin.Y + rowHeight));
            var mode = SegmentSlider.Draw(segmentRect, Loc.T(L.AetherStream.PlayNow),
                Loc.T(L.AetherStream.AddToQueue), queueOnAdd ? 1 : 0, ref composerModeAnimation, ui.Accent,
                ui.MutedInk);
            queueOnAdd = mode == 1;
        }

        ImGui.SetCursorScreenPos(rowOrigin);
        ImGui.Dummy(new Vector2(width, rowHeight));

        if (!submitted || !canSubmit)
        {
            return;
        }

        if (suggesting)
        {
            watchAlong.SuggestQueueItem(urlInput.Trim());
            urlInput = string.Empty;
            return;
        }

        SubmitUrl();
    }

    private void DrawWatchPartySection(float width, float scale)
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
        ui.SectionLabel(Loc.T(L.AetherStream.WatchPartyHeader));

        if (watchAlong.IsAwaitingApproval)
        {
            DrawPartyAwaiting(width, scale);
            return;
        }

        if (watchAlong.IsViewing)
        {
            DrawPartyViewing(width, scale);
            return;
        }

        if (watchAlong.IsHosting)
        {
            DrawPartyHosting(width, scale);
            return;
        }

        DrawPartyIdle(width, scale);
    }

    private void DrawPartyIdle(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var buttonHeight = PartyButtonHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var half = (width - gap) * 0.5f;
        var startRect = new Rect(origin, origin + new Vector2(half, buttonHeight));
        var joinRect = new Rect(new Vector2(origin.X + width - half, origin.Y),
            new Vector2(origin.X + width, origin.Y + buttonHeight));
        if (ui.PillButton(startRect, Loc.T(L.AetherStream.StartParty), true, "aetherstream.party.start"))
        {
            watchAlong.OpenParty();
        }

        if (ui.GhostButton(joinRect, Loc.T(L.AetherStream.JoinStream)))
        {
            nearbyRefreshTimer = NearbyRefreshIntervalSeconds;
            router.Push(AetherStreamScreen.Join);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, buttonHeight));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ui.HelpText(Loc.T(L.AetherStream.WatchPartyHint));
    }

    private void DrawPartyAwaiting(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var captionHeight = 30f * scale;
        LoadingPulse.Caption(new Vector2(origin.X + width * 0.5f, origin.Y + captionHeight * 0.5f), ui.MutedInk,
            ui.Accent, Loc.T(L.AetherStream.JoinWaitingApproval), 1f, TextStyles.Subheadline.Scale);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, captionHeight));
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));

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
            ImGui.Dummy(new Vector2(width, lineHeight + Metrics.Space.Sm * scale));
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
        }

        ImGui.SetCursorScreenPos(buttonOrigin);
        ImGui.Dummy(new Vector2(width, buttonHeight));
    }

    private void DrawPartyHosting(float width, float scale)
    {
        DrawPartyRoster(width, scale, canKick: true);

        var buttonOrigin = ImGui.GetCursorScreenPos();
        var endRect = new Rect(buttonOrigin, buttonOrigin + new Vector2(width, PartyButtonHeight * scale));
        if (ui.DangerGhostButton(endRect, Loc.T(L.AetherStream.EndParty)))
        {
            watchAlong.Leave();
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

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
    }

    private void DrawRosterLabel(string label, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        Typography.Draw(ImGui.GetWindowDrawList(), origin, label, ui.MutedInk, TextStyles.Caption1);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Typography.LineHeight(TextStyles.Caption1) + Metrics.Space.Xs * scale));
    }

    private void DrawWatcherRow(float width, float scale, WatchAlongParticipant participant, bool kickable)
    {
        var origin = ImGui.GetCursorScreenPos();
        var row = new Rect(origin, origin + new Vector2(width, PartyRowHeight * scale));
        var drawList = ImGui.GetWindowDrawList();
        var avatarRadius = 16f * scale;
        var avatarCenter = new Vector2(row.Min.X + avatarRadius, row.Center.Y);
        AvatarView.DrawRemote(drawList, avatarCenter, avatarRadius, theme, participant.Name, participant.World,
            participant.AvatarUrl, remoteImages, lodestone, 0.7f, 20);

        var textLeft = avatarCenter.X + avatarRadius + Metrics.Space.Md * scale;
        var textRight = row.Max.X;
        if (kickable)
        {
            var kickCenter = new Vector2(row.Max.X - 14f * scale, row.Center.Y);
            if (ui.IconButton(kickCenter, 12f * scale, FontAwesomeIcon.Times.ToIconString(), ui.MutedInk,
                    AppSkin.Transparent, 0.55f, Loc.T(L.AetherStream.WatchingKick)))
            {
                watchAlong.KickParticipant(participant.UserId);
            }

            textRight = kickCenter.X - 20f * scale;
        }

        var nameHeight = Typography.LineHeight(TextStyles.Body);
        Marquee.DrawLeft(drawList, "aetherstream.watcher." + participant.UserId, participant.DisplayName, textLeft,
            row.Center.Y - nameHeight * 0.5f, textRight - textLeft, TextStyles.Body, ui.TitleInk,
            UiInteract.Hover(row.Min, row.Max));

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, PartyRowHeight * scale));
    }

    private void SubmitUrl()
    {
        var url = urlInput.Trim();
        urlInput = string.Empty;
        SubmitEntry(new VideoQueueEntry(url, url, string.Empty, null, null));
    }

    private void SubmitLocalFile(string path)
    {
        SubmitEntry(new VideoQueueEntry(path, Path.GetFileNameWithoutExtension(path),
            Loc.T(L.AetherStream.LocalFileSource), null, null));
    }

    private void SubmitEntry(VideoQueueEntry entry)
    {
        if (queueOnAdd)
        {
            queue.Add(entry);
            return;
        }

        queue.PlayNow(entry);
    }
}
