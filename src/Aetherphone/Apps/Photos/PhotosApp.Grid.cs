using System.Diagnostics;
using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp
{
    private void DrawRoot(Rect area)
    {
        DrawNavBar(area, DisplayName, null);
        var scale = ImGuiHelpers.GlobalScale;
        var pad = 14f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var segBar = new Rect(new Vector2(area.Min.X + pad, top + 4f * scale),
            new Vector2(area.Max.X - pad, top + 4f * scale + SegmentHeight * scale));
        segmentLabels[0] = Loc.T(L.Photos.Library);
        segmentLabels[1] = Loc.T(L.Photos.Albums);
        var picked = SegmentStrip.Draw("photos.segment", segBar, segmentLabels, segment, ui.Palette);
        if (picked != segment)
        {
            segment = picked;
            resetScroll = true;
        }

        var body = new Rect(new Vector2(area.Min.X, segBar.Max.Y + 6f * scale), area.Max);
        if (segment == 0)
        {
            UiAnchors.Report("photos.grid", body);
            if (entries.Length == 0)
            {
                DrawEmpty(body);
                return;
            }

            DrawPhotoGrid(body, 0, entries.Length);
            DrawOpenFolder(body);
            return;
        }

        if (entries.Length == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawAlbumsGrid(body);
        DrawNewAlbumFab(body);
    }

    private void DrawAlbum(Rect area, int key)
    {
        var scale = ImGuiHelpers.GlobalScale;
        int start;
        int count;
        string title;
        if (key == PhotoView.RecentsKey)
        {
            start = 0;
            count = entries.Length;
            title = Loc.T(L.Photos.Recents);
        }
        else if (TryFindAlbum(key, out var album))
        {
            start = album.Start;
            count = album.Count;
            title = Capitalize(album.Month.ToString("MMMM yyyy", Loc.Culture));
        }
        else if (key < 0)
        {
            DrawCustomAlbumView(area, key);
            return;
        }
        else
        {
            router.Pop(false);
            return;
        }

        DrawNavBar(area, title, back);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (count == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawPhotoGrid(body, start, count);
    }
    
    private void DrawCustomAlbumView(Rect area, int key)
    {
        if (showAlbumPicker && pickerAlbumKey == key)
        {
            DrawAlbumPicker(area, key);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        if (!TryFindCustomAlbum(key, out var album))
        {
            router.Pop(false);
            return;
        }

        DrawNavBar(area, album.Name, back);
        if (ui.HeaderAction(area, Loc.T(L.Photos.AddPhotos), entries.Length > 0))
        {
            pickerAlbumKey = key;
            showAlbumPicker = true;
            pickerSelection.Clear();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        var paths = SortedCustomAlbumPaths(key);
        if (paths.Length == 0)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Photos.EmptyAlbum), ui.MutedInk, TextStyles.Body);
            return;
        }

        DrawCustomAlbumGrid(body, paths, key);
    }

    private void DrawCreateAlbumPage(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;

        Action cancel = () =>
        {
            renaming = false;
            newAlbumDraft = string.Empty;
            renameAlbumDraft = string.Empty;
            router.Pop();
        };

        DrawNavBar(area, renaming ? Loc.T(L.Photos.Rename) : Loc.T(L.Photos.CreateAlbum), cancel);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (renaming)
            DrawRenameAlbumSheet(body, cancel);
        else
            DrawCreateAlbumSheet(body, cancel);
    }

    private void DrawCreateAlbumSheet(Rect body, Action cancel)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 8f * scale)))
        using (ImRaii.Child("##createSheet", body.Size, false, ImGuiWindowFlags.NoBackground))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
                Typography.Plain(Loc.T(L.Photos.AlbumName));

            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = 34f * scale;
            var drawList = ImGui.GetWindowDrawList();
            Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
                ImGui.GetColorU32(ui.FieldSurface));
            ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
                origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
            ImGui.SetNextItemWidth(width - 24f * scale);
            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                       .Push(ImGuiCol.Text, ui.TitleInk))
            {
                ImGui.InputText("##newAlbumName", ref newAlbumDraft, 64);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height));

            ImGui.Dummy(new Vector2(0f, 18f * scale));
            var canCreate = newAlbumDraft.Trim().Length > 0
                && !customAlbumOrder.Contains(newAlbumDraft.Trim(), StringComparer.OrdinalIgnoreCase);

            var accent = canCreate ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.4f);
            using (ImRaii.PushColor(ImGuiCol.Button, accent)
                       .Push(ImGuiCol.ButtonHovered,
                           canCreate ? Palette.Mix(ui.Accent, new Vector4(1f, 1f, 1f, 1f), 0.14f) : accent)
                       .Push(ImGuiCol.ButtonActive, accent)
                       .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, canCreate ? 1f : 0.72f)))
            {
                if (ImGui.Button(Loc.T(L.Photos.CreateAlbum), new Vector2(-1f, 38f * scale)) && canCreate)
                {
                    CreateCustomAlbumInternal(newAlbumDraft);
                    newAlbumDraft = string.Empty;
                    cancel();
                }
            }

            if (!canCreate && newAlbumDraft.Trim().Length > 0)
            {
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, ui.Palette.Accent))
                    Typography.Wrapped(Loc.T(L.Photos.AlbumExists));
            }
        }
    }

    private void DrawRenameAlbumSheet(Rect body, Action cancel)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f * scale, 8f * scale)))
        using (ImRaii.Child("##renameSheet", body.Size, false, ImGuiWindowFlags.NoBackground))
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ui.MutedInk))
                Typography.Plain(Loc.T(L.Photos.AlbumName));

            var origin = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            var height = 34f * scale;
            var drawList = ImGui.GetWindowDrawList();
            Squircle.Fill(drawList, origin, new Vector2(origin.X + width, origin.Y + height), 9f * scale,
                ImGui.GetColorU32(ui.FieldSurface));
            ImGui.SetCursorScreenPos(new Vector2(origin.X + 12f * scale,
                origin.Y + height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
            ImGui.SetNextItemWidth(width - 24f * scale);
            using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                       .Push(ImGuiCol.Text, ui.TitleInk))
            {
                ImGui.InputText("##renameAlbumName", ref renameAlbumDraft, 64);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, height));

            ImGui.Dummy(new Vector2(0f, 18f * scale));
            var canRename = renameAlbumDraft.Trim().Length > 0;
            if (canRename)
            {
                var found = customAlbums.FirstOrDefault(c => c.Key == renameAlbumKey);
                if (found.Name is not null && string.Equals(found.Name, renameAlbumDraft.Trim(), StringComparison.OrdinalIgnoreCase))
                    canRename = false;
                else if (found.Name is not null && customAlbumOrder.Contains(renameAlbumDraft.Trim(), StringComparer.OrdinalIgnoreCase))
                    canRename = false;
            }

            var accent = canRename ? ui.Accent : Palette.WithAlpha(ui.Accent, 0.4f);
            using (ImRaii.PushColor(ImGuiCol.Button, accent)
                       .Push(ImGuiCol.ButtonHovered,
                           canRename ? Palette.Mix(ui.Accent, new Vector4(1f, 1f, 1f, 1f), 0.14f) : accent)
                       .Push(ImGuiCol.ButtonActive, accent)
                       .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, canRename ? 1f : 0.72f)))
            {
                if (ImGui.Button(Loc.T(L.Photos.Rename), new Vector2(-1f, 38f * scale)) && canRename)
                {
                    RenameCustomAlbumInternal(renameAlbumKey, renameAlbumDraft);
                    renameAlbumDraft = string.Empty;
                    cancel();
                }
            }
        }
    }

    private void DrawEmpty(Rect body) =>
        EmptyState.Draw(body, ui, FontAwesomeIcon.Image, Loc.T(L.Photos.NoPhotos), Loc.T(L.Photos.UseCameraHint));

    private void DrawPhotoGrid(Rect body, int start, int count)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gridKey = ImGui.GetID("##photoGrid");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##photoGrid", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            var surface = DragScrollHost.Begin(gridKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var side = 2f * scale;
            var gap = 3f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - side * 2f - gap * (Columns - 1)) / Columns;
            var total = LayoutBands(start, count, cell, gap, scale);
            var drawList = ImGui.GetWindowDrawList();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + 60f * scale;
            for (var index = 0; index < bands.Count; index++)
            {
                var band = bands[index];
                if (band.Top + band.Height < scrollY - margin || band.Top > scrollY + viewHeight + margin)
                {
                    continue;
                }

                var screenTop = origin.Y + band.Top;
                if (band.Header)
                {
                    DrawSectionHeader(drawList, new Vector2(origin.X + side + 4f * scale, screenTop),
                        avail - side * 2f - 8f * scale, band, scale);
                    continue;
                }

                DrawPhotoRow(drawList, band, origin.X + side, screenTop, cell, gap, start, count);
            }

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, total));
        }
    }

    private float LayoutBands(int start, int count, float cell, float gap, float scale)
    {
        bands.Clear();
        var headerHeight = 40f * scale;
        var rowStride = cell + gap;
        var blockGap = 10f * scale;
        var y = 6f * scale;
        var index = start;
        var end = start + count;
        while (index < end)
        {
            var day = entries[index].Taken.Date;
            var dayStart = index;
            while (index < end && entries[index].Taken.Date == day)
            {
                index++;
            }

            var dayCount = index - dayStart;
            bands.Add(new GridBand
            {
                Header = true,
                Day = entries[dayStart].Taken,
                DayCount = dayCount,
                Top = y,
                Height = headerHeight,
            });
            y += headerHeight;
            var rows = (dayCount + Columns - 1) / Columns;
            for (var row = 0; row < rows; row++)
            {
                var rowStart = dayStart + row * Columns;
                var rowCount = Math.Min(Columns, dayStart + dayCount - rowStart);
                bands.Add(new GridBand
                {
                    Header = false,
                    PhotoStart = rowStart,
                    PhotoCount = rowCount,
                    Top = y,
                    Height = cell,
                });
                y += rowStride;
            }

            y += blockGap;
        }

        return y + 6f * scale;
    }

    private void DrawSectionHeader(ImDrawListPtr drawList, Vector2 topLeft, float width, GridBand band, float scale)
    {
        var label = DayLabel(band.Day);
        var count = Loc.Plural(L.Photos.Count, band.DayCount);
        var centerY = topLeft.Y + 40f * scale * 0.5f + 3f * scale;
        var countSize = Typography.Measure(count, TextStyles.Footnote);
        var nameMax = MathF.Max(24f * scale, width - countSize.X - 12f * scale);
        var name = Typography.FitText(label, nameMax, TextStyles.Headline);
        var nameSize = Typography.Measure(name, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(topLeft.X, centerY - nameSize.Y * 0.5f), name, ui.TitleInk,
            TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(topLeft.X + width - countSize.X, centerY - countSize.Y * 0.5f), count,
            ui.MutedInk, TextStyles.Footnote);
    }

    private void DrawPhotoRow(ImDrawListPtr drawList, GridBand band, float leftX, float top, float cell, float gap,
        int sliceStart, int sliceCount)
    {
        for (var column = 0; column < band.PhotoCount; column++)
        {
            var absolute = band.PhotoStart + column;
            var min = new Vector2(leftX + column * (cell + gap), top);
            var max = new Vector2(min.X + cell, min.Y + cell);
            var hovered = UiInteract.Hover(min, max);
            PhotosChrome.Thumbnail(drawList, GetThumbnail(entries[absolute].Path), min, max, hovered, ui.FieldSurface);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                OpenViewer(sliceStart, sliceCount, absolute);
            }
        }
    }

    private void DrawAlbumsGrid(Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var albumsKey = ImGui.GetID("##photoAlbums");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14f * scale, 6f * scale)))
        using (var child = ImRaii.Child("##photoAlbums", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
            {
                return;
            }

            var surface = DragScrollHost.Begin(albumsKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var gap = 12f * scale;
            const int columns = 2;
            var tileWidth = (width - gap) / columns;
            var coverHeight = tileWidth;
            var cardHeight = coverHeight + 42f * scale;
            var drawList = ImGui.GetWindowDrawList();
            var total = 1 + customAlbums.Count + albums.Count;
            for (var index = 0; index < total; index++)
            {
                var column = index % columns;
                var rowIndex = index / columns;
                var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + rowIndex * (cardHeight + gap));
                var rect = new Rect(min, new Vector2(min.X + tileWidth, min.Y + cardHeight));
                if (index == 0)
                {
                    if (DrawAlbumCard(drawList, rect, Loc.T(L.Photos.Recents), 0, entries.Length, coverHeight, scale))
                    {
                        OpenAlbum(PhotoView.RecentsKey);
                    }

                    continue;
                }

                var customIndex = index - 1;
                if (customIndex < customAlbums.Count)
                {
                    DrawCustomAlbumCard(drawList, rect, customAlbums[customIndex], coverHeight, scale, body);
                    continue;
                }

                var monthIndex = index - 1 - customAlbums.Count;
                var album = albums[monthIndex];
                var title = Capitalize(album.Month.ToString("MMMM yyyy", Loc.Culture));
                if (DrawAlbumCard(drawList, rect, title, album.Start, album.Count, coverHeight, scale))
                {
                    OpenAlbum(album.Key);
                }
            }

            var rows = (total + columns - 1) / columns;
            var heightTotal = rows * cardHeight + (rows - 1) * gap;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(width, heightTotal + 12f * scale));
        }
    }

    private void DrawOpenFolder(Rect rect)
    {
        if (ComposeFab.Draw(rect, "##openFolderFab", Accent, FontAwesomeIcon.Folder.ToIconString(),
                             Loc.T(L.Photos.OpenFolder), "photos.openFolder"))
        {
            UrlActions.OpenFolder(library.DirectoryPath);
        }
    }

    private bool DrawAlbumCard(ImDrawListPtr drawList, Rect rect, string title, int coverStart, int coverCount,
        float coverHeight, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var coverMax = new Vector2(rect.Max.X, rect.Min.Y + coverHeight);
        var rounding = 16f * scale;
        var shadow = new Vector2(0f, 3f * scale);
        drawList.AddRectFilled(rect.Min + shadow, coverMax + shadow,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), rounding, ImDrawFlags.RoundCornersAll);
        var cover = coverCount > 0 ? GetThumbnail(entries[coverStart].Path) : null;
        if (cover is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(cover.Size);
            drawList.AddImageRounded(cover.Handle, rect.Min, coverMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddRectFilled(rect.Min, coverMax, ImGui.GetColorU32(ui.FieldSurface), rounding,
                ImDrawFlags.RoundCornersAll);
            ProgressRing.Sweep(new Vector2(rect.Center.X, rect.Min.Y + coverHeight * 0.5f), 10f * scale, 2f * scale,
                ui.MutedInk, 900.0, 1.8f, 0.9f);
        }

        Material.Edge(drawList, rect.Min, coverMax, rounding, scale, hovered ? 1f : 0.7f);
        if (hovered)
        {
            drawList.AddRectFilled(rect.Min, coverMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), rounding,
                ImDrawFlags.RoundCornersAll);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textTop = coverMax.Y + 7f * scale;
        var name = Typography.FitText(title, rect.Width - 4f * scale, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop), name, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var countLabel = Loc.Plural(L.Photos.Count, coverCount);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop + 19f * scale), countLabel, ui.MutedInk,
            TextStyles.Footnote);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void OpenAlbum(int key) => router.Push(PhotoView.Album(key));
    
    private void DrawNewAlbumFab(Rect rect)
    {
        if (ComposeFab.Draw(rect, "##newAlbumFab", Accent, FontAwesomeIcon.Plus.ToIconString(),
                             Loc.T(L.Photos.CreateAlbum), "photos.newAlbum"))
        {
            renaming = false;
            newAlbumDraft = string.Empty;
            router.Push(PhotoView.CreateAlbum());
        }
    }

    private void DrawCustomAlbumCard(ImDrawListPtr drawList, Rect rect, CustomAlbum album, float coverHeight,
        float scale, Rect screen)
    {
        var coverCoverMax = new Vector2(rect.Max.X, rect.Min.Y + coverHeight);
        var rounding = 16f * scale;
        var shadow = new Vector2(0f, 3f * scale);
        drawList.AddRectFilled(rect.Min + shadow, coverCoverMax + shadow,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), rounding, ImDrawFlags.RoundCornersAll);

        string? coverPath = null;
        if (customAlbumPhotos.TryGetValue(album.Name, out var photos) && photos.Count > 0)
        {
            coverPath = photos[0];
        }

        if (coverPath is not null && GetThumbnail(coverPath) is { } cover)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(cover.Size);
            drawList.AddImageRounded(cover.Handle, rect.Min, coverCoverMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            drawList.AddRectFilled(rect.Min, coverCoverMax, ImGui.GetColorU32(ui.FieldSurface), rounding,
                ImDrawFlags.RoundCornersAll);
            AppSkin.Icon(drawList, new Vector2(rect.Center.X, rect.Min.Y + coverHeight * 0.5f),
                FontAwesomeIcon.Images.ToIconString(), ui.MutedInk, 1.2f);
        }

        Material.Edge(drawList, rect.Min, coverCoverMax, rounding, scale, 0.7f);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            drawList.AddRectFilled(rect.Min, coverCoverMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f)), rounding,
                ImDrawFlags.RoundCornersAll);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textTop = coverCoverMax.Y + 7f * scale;
        var name = Typography.FitText(album.Name, rect.Width - 4f * scale, TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop), name, ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        var countLabel = Loc.Plural(L.Photos.Count, album.Count);
        Typography.Draw(drawList, new Vector2(rect.Min.X + 2f * scale, textTop + 19f * scale), countLabel, ui.MutedInk,
            TextStyles.Footnote);

        // Right-click to open context menu
        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            var pos = ImGui.GetMousePos();
            albumMenu.Toggle("custom:" + album.Key, new Rect(pos, pos + new Vector2(1f, 1f)));
        }

        // Context menu
        var menuId = "custom:" + album.Key;
        if (albumMenu.IsOpenFor(menuId))
        {
            albumMenu.Gate();
            DrawCustomAlbumContextMenu(album.Key, screen);
        }

        // Tap to open
        if (UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            OpenAlbum(album.Key);
        }
    }
    
    private void DrawCustomAlbumContextMenu(int key, Rect screen)
    {
        DropdownMenu.Item[] items =
        [
            new(Loc.T(L.Photos.Rename), FontAwesomeIcon.Pen.ToIconString()),
            new(Loc.T(L.Photos.DeleteAlbum), FontAwesomeIcon.Trash.ToIconString(), Danger: true),
        ];
        var picked = albumMenu.Draw(screen, frameTheme, items, out var action);
        if (picked < 0)
            return;

        if (action == DropdownMenu.RowAction.Delete || (action == DropdownMenu.RowAction.Select && picked == 1))
        {
            var found = customAlbums.FirstOrDefault(c => c.Key == key);
            if (found.Name is null)
                return;
            confirm.Ask(new ConfirmRequest
            {
                Message = Loc.T(L.Photos.DeleteAlbumConfirm, found.Name) + "\n" + Loc.T(L.Photos.DeleteAlbumBody),
                ConfirmLabel = Loc.T(L.Photos.DeleteAlbum),
                CancelLabel = Loc.T(L.Common.Cancel),
                Confirm = () => DeleteCustomAlbumInternal(key),
            });
        }
        else if (picked == 0)
        {
            renaming = true;
            renameAlbumKey = key;
            renameAlbumDraft = string.Empty;
            var found = customAlbums.FirstOrDefault(c => c.Key == key);
            if (found.Name is not null)
                renameAlbumDraft = found.Name;
            router.Push(PhotoView.CreateAlbum());
        }
    }
    
    private void DrawAlbumPicker(Rect area, int key)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (!TryFindCustomAlbum(key, out var album))
        {
            showAlbumPicker = false;
            return;
        }

        DrawNavBar(area, string.Format(Loc.Culture, "{0} — {1}", album.Name, Loc.T(L.Photos.AddPhotos)), () =>
        {
            showAlbumPicker = false;
            pickerSelection.Clear();
        });

        if (ui.HeaderAction(area, Loc.T(L.Photos.Done), pickerSelection.Count > 0) && pickerSelection.Count > 0)
        {
            AddPhotosToCustomAlbum(key, pickerSelection.ToArray());
            showAlbumPicker = false;
            pickerSelection.Clear();
        }

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        if (entries.Length == 0)
        {
            DrawEmpty(body);
            return;
        }

        DrawAlbumPickerGrid(body, key);
    }
    
    private void DrawAlbumPickerGrid(Rect body, int albumKey)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gridKey = ImGui.GetID("##albumPicker");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##albumPicker", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
                return;

            var surface = DragScrollHost.Begin(gridKey);
            surface.JumpToTop();

            var origin = ImGui.GetCursorScreenPos();
            var side = 2f * scale;
            var gap = 3f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - side * 2f - gap * (Columns - 1)) / Columns;
            var drawList = ImGui.GetWindowDrawList();

            var inAlbum = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (customAlbumPhotos.TryGetValue(
                    customAlbums.FirstOrDefault(c => c.Key == albumKey).Name ?? string.Empty, out var existing))
            {
                inAlbum.UnionWith(existing);
            }

            for (var index = 0; index < entries.Length; index++)
            {
                var column = index % Columns;
                var rowIndex = index / Columns;
                var min = new Vector2(origin.X + side + column * (cell + gap),
                    origin.Y + 6f * scale + rowIndex * (cell + gap));
                var max = new Vector2(min.X + cell, min.Y + cell);
                var path = entries[index].Path;
                var alreadyInAlbum = inAlbum.Contains(path);
                var isSelected = pickerSelection.Contains(path);
                var canSelect = !alreadyInAlbum;
                var effectiveHovered = canSelect && UiInteract.Hover(min, max);

                PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, effectiveHovered, ui.FieldSurface);

                if (alreadyInAlbum)
                {
                    drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 7f * scale);
                    var checkCenter = new Vector2(max.X - 14f * scale, min.Y + 14f * scale);
                    AppSkin.Icon(drawList, checkCenter, FontAwesomeIcon.Check.ToIconString(),
                        new Vector4(0.4f, 0.8f, 0.4f, 0.8f), 0.8f);
                }
                else if (isSelected)
                {
                    drawList.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 7f * scale);
                    var radius = 11f * scale;
                    var badgeCenter = new Vector2(max.X - radius - 6f * scale, min.Y + radius + 6f * scale);
                    drawList.AddCircleFilled(badgeCenter, radius, ImGui.GetColorU32(ui.Accent), 20);
                    var order = pickerSelection.IndexOf(path) + 1;
                    Typography.DrawCentered(drawList, badgeCenter, order.ToString(Loc.Culture),
                        new Vector4(1f, 1f, 1f, 1f), TextStyles.FootnoteEmphasized);
                }

                if (effectiveHovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (isSelected)
                            pickerSelection.Remove(path);
                        else
                            pickerSelection.Add(path);
                    }
                }
            }

            var rows = (entries.Length + Columns - 1) / Columns;
            var totalHeight = rows * (cell + gap) + 12f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, totalHeight));
        }
    }
    
    private void DrawCustomAlbumGrid(Rect body, string[] paths, int albumKey)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gridKey = ImGui.GetID("##customAlbumGrid");
        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##customAlbumGrid", body.Size, false,
                   DragScrollHost.ScrollFlags(ImGuiWindowFlags.NoBackground)))
        {
            if (!child)
                return;

            var surface = DragScrollHost.Begin(gridKey);
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var side = 2f * scale;
            var gap = 3f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - side * 2f - gap * (Columns - 1)) / Columns;
            var drawList = ImGui.GetWindowDrawList();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + 60f * scale;

            for (var index = 0; index < paths.Length; index++)
            {
                var column = index % Columns;
                var rowIndex = index / Columns;
                var top = 6f * scale + rowIndex * (cell + gap);

                if (top + cell < scrollY - margin || top > scrollY + viewHeight + margin)
                    continue;

                var min = new Vector2(origin.X + side + column * (cell + gap), origin.Y + top);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var path = paths[index];
                var hovered = UiInteract.Hover(min, max);

                PhotosChrome.Thumbnail(drawList, GetThumbnail(path), min, max, hovered, ui.FieldSurface);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (UiInteract.Click(min, max, hovered))
                {
                    OpenViewerFromPaths(paths, index);
                }

                // Right-click to remove from album
                if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    var removePath = path;
                    confirm.Ask(new ConfirmRequest
                    {
                        Message = Loc.T(L.Photos.RemoveFromAlbum),
                        ConfirmLabel = Loc.T(L.Photos.RemoveFromAlbum),
                        CancelLabel = Loc.T(L.Common.Cancel),
                        Confirm = () => RemovePhotoFromCustomAlbum(albumKey, removePath),
                    });
                }
            }

            var rows = (paths.Length + Columns - 1) / Columns;
            var totalHeight = rows * (cell + gap) + 12f * scale;
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, totalHeight));
        }
    }

    private bool TryFindAlbum(int key, out MonthAlbum album)
    {
        for (var index = 0; index < albums.Count; index++)
        {
            if (albums[index].Key == key)
            {
                album = albums[index];
                return true;
            }
        }

        album = default;
        return false;
    }

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Loc.Culture) + text.Substring(1);
}
