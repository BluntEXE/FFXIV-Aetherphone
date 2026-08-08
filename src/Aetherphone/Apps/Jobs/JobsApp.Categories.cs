using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Jobs;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Jobs;

internal sealed partial class JobsApp
{
    private const float EditorWidth = 252f;
    private const float EditorTitleHeight = 18f;
    private const float EditorFieldHeight = 30f;
    private const int CategoryNameMaxLength = 24;
    private const float CategoryDragHandleRadius = 9f;
    private const float DragThreshold = 7f;

    private static readonly List<JobsCategory> NoCategories = new();

    private bool categoryEditorOpen;
    private int categoryEditorIndex = -1;
    private int categoryEditorGearsetId = -1;
    private int categoryEditorOpenedFrame;
    private string categoryEditorName = string.Empty;
    private bool focusCategoryField;

    // Header rect places the drop highlight, span rect claims the vertical band that counts as
    // "over this category". Spans, not header centers, because section heights swing with gearset
    // count and the midpoint between two headers is nowhere near the boundary between their slots.
    private readonly List<(int CategoryIndex, Rect Header, Rect Span)> categoryDropSlots = new();
    private int categoryDragIndex = -1;
    private int categoryDropIndex = -1;
    private Vector2 categoryDragPressPos;
    private bool categoryDragActive;

    private int jobDragCategoryIndex = -1;
    private int jobDragIndex = -1;
    private int jobDragFromGearsetId = -1;
    private int jobDragToGearsetId = -1;
    private Vector2 jobDragStart;
    private float jobDragOffset;
    private bool jobDragActive;

    private List<JobsCategory> CurrentCategories()
    {
        var contentId = characterWatch.CurrentContentId;
        return contentId != 0 && configuration.JobsCategoriesByCharacter.TryGetValue(contentId, out var categories)
            ? categories
            : NoCategories;
    }

    private List<JobsCategory> CategoriesForWrite()
    {
        var contentId = characterWatch.CurrentContentId;
        if (contentId == 0)
        {
            return NoCategories;
        }

        if (!configuration.JobsCategoriesByCharacter.TryGetValue(contentId, out var categories))
        {
            categories = new List<JobsCategory>();
            configuration.JobsCategoriesByCharacter[contentId] = categories;
        }

        return categories;
    }

    private void DrawCategoriesMenu(Rect content, PhoneTheme theme)
    {
        if (!menu.IsOpenFor(CategoryMenuId))
        {
            return;
        }

        var categories = CurrentCategories();
        var items = new DropdownMenu.Item[categories.Count + 1];
        for (var index = 0; index < categories.Count; index++)
        {
            items[index] = new DropdownMenu.Item(categories[index].Name, CanEdit: true, CanDelete: true);
        }

        items[categories.Count] = new DropdownMenu.Item(Loc.T(L.Jobs.NewCategory),
            Glyph: FontAwesomeIcon.FolderPlus.ToIconString());

        var picked = menu.Draw(content, theme, items, out var rowAction);
        if (picked < 0)
        {
            return;
        }

        if (picked == categories.Count)
        {
            OpenCategoryEditor(-1, -1);
            return;
        }

        if (rowAction == DropdownMenu.RowAction.Delete)
        {
            DeleteCategory(picked);
            return;
        }

        OpenCategoryEditor(picked, -1);
    }

    // A real InvisibleButton rather than a bare UiInteract.Hover check: Dear ImGui starts its own
    // native window-drag whenever a press lands on the window background with no item active,
    // regardless of LockPosition (that only adds NoMove), so without an item under the press the
    // handle would drag the whole phone. UiInteract still owns the gating, since raw ImGui item
    // state ignores InputBlocked and the overlay reservation.
    private void DrawCategoryDragHandle(Rect headerRect, int categoryIndex, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var radius = CategoryDragHandleRadius * scale;
        var center = new Vector2(headerRect.Max.X - radius - 2f * scale, headerRect.Center.Y);
        var half = new Vector2(radius);
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(center - half);
        ImGui.InvisibleButton($"##categoryDragHandle{categoryIndex}", half * 2f);
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(center - half, center + half);
        var activated = hovered && ImGui.IsItemActivated();
        ImGui.SetCursorScreenPos(cursor);

        categoryDropSlots.Add((categoryIndex, headerRect, headerRect));

        var dragging = categoryDragIndex == categoryIndex;
        var tint = dragging || hovered
            ? ui.Accent
            : Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.6f);
        AppSkin.Icon(drawList, center, FontAwesomeIcon.GripLinesVertical.ToIconString(), tint, 0.6f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (categoryDragIndex < 0 && activated)
        {
            categoryDragIndex = categoryIndex;
            categoryDragPressPos = ImGui.GetMousePos();
            categoryDragActive = false;
        }
    }

    private void SealCategoryDropSlot(float bottom)
    {
        var last = categoryDropSlots.Count - 1;
        if (last < 0)
        {
            return;
        }

        var slot = categoryDropSlots[last];
        categoryDropSlots[last] = (slot.CategoryIndex, slot.Header,
            new Rect(slot.Span.Min, new Vector2(slot.Span.Max.X, bottom)));
    }

    // Runs inside the app surface so the drop highlight inherits its clip rect, and after every
    // custom section has registered its slot, since the target search needs all of them.
    private void UpdateCategoryDrag(float scale)
    {
        if (categoryDragIndex < 0)
        {
            return;
        }

        if (!categoryDragActive && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
            Vector2.Distance(ImGui.GetMousePos(), categoryDragPressPos) > DragThreshold * scale)
        {
            categoryDragActive = true;
        }

        if (!categoryDragActive)
        {
            return;
        }

        categoryDropIndex = CategoryDropTarget(ImGui.GetMousePos().Y);
        DrawCategoryDropHighlight(categoryDropIndex, scale);
    }

    // Unconditional for the same reason as FinishJobDrag: the surface stops drawing on logout or
    // app close, and nothing there would ever release the gesture.
    private void FinishCategoryDrag()
    {
        if (categoryDragIndex < 0 || ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        if (categoryDragActive)
        {
            ReorderCategory(categoryDragIndex, categoryDropIndex);
        }

        ResetCategoryDrag();
    }

    private void ResetCategoryDrag()
    {
        categoryDragIndex = -1;
        categoryDropIndex = -1;
        categoryDragActive = false;
    }

    // -1 when the pointer is outside every custom section, which cancels the drop rather than
    // snapping to whichever header happens to be nearest.
    private int CategoryDropTarget(float mouseY)
    {
        for (var index = 0; index < categoryDropSlots.Count; index++)
        {
            var span = categoryDropSlots[index].Span;
            if (mouseY >= span.Min.Y && mouseY < span.Max.Y)
            {
                return categoryDropSlots[index].CategoryIndex;
            }
        }

        return -1;
    }

    private void DrawCategoryDropHighlight(int categoryIndex, float scale)
    {
        if (categoryIndex < 0)
        {
            return;
        }

        for (var index = 0; index < categoryDropSlots.Count; index++)
        {
            if (categoryDropSlots[index].CategoryIndex != categoryIndex)
            {
                continue;
            }

            var rect = categoryDropSlots[index].Header;
            ImGui.GetWindowDrawList().AddRectFilled(rect.Min, rect.Max,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.16f)), 6f * scale);
            return;
        }
    }

    // A true move (remove then re-insert), not an adjacent swap: one gesture can cross several
    // categories at once.
    private void ReorderCategory(int fromIndex, int toIndex)
    {
        if (characterWatch.CurrentContentId == 0)
        {
            return;
        }

        var categories = CategoriesForWrite();
        if (fromIndex < 0 || fromIndex >= categories.Count || toIndex < 0 || toIndex >= categories.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        var item = categories[fromIndex];
        categories.RemoveAt(fromIndex);
        categories.Insert(toIndex, item);
        configuration.Save();
        Rebuild();
    }

    // Returns whether the handle is hovered, so DrawJobRow can keep that area out of the row's own
    // hover and equip-click handling. InvisibleButton and UiInteract gating as in
    // DrawCategoryDragHandle.
    private bool DrawJobDragHandle(ImDrawListPtr drawList, Vector2 center, float radius, int categoryIndex,
        int rowIndex)
    {
        var half = new Vector2(radius);
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(center - half);
        ImGui.InvisibleButton($"##jobDragHandle{categoryIndex}_{rowIndex}", half * 2f);
        var hovered = ImGui.IsItemHovered() && UiInteract.Hover(center - half, center + half);
        var activated = hovered && ImGui.IsItemActivated();
        ImGui.SetCursorScreenPos(cursor);

        var dragging = jobDragCategoryIndex == categoryIndex && jobDragIndex == rowIndex;
        var tint = dragging || hovered ? ui.Accent : Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.6f);
        AppSkin.Icon(drawList, center, FontAwesomeIcon.GripLines.ToIconString(), tint, 0.55f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (jobDragIndex < 0 && activated)
        {
            jobDragCategoryIndex = categoryIndex;
            jobDragIndex = rowIndex;
            jobDragFromGearsetId = -1;
            jobDragToGearsetId = -1;
            jobDragStart = ImGui.GetMousePos();
            jobDragOffset = 0f;
            jobDragActive = false;
        }

        return hovered;
    }

    // Tracks the live gesture while the dragged row's own card is on screen, resolving it down to
    // the pair of gearset ids that FinishJobDrag will commit. Travel is clamped to the card so the
    // lifted row cannot paint over a neighbouring section, and so the preview matches the drop.
    private void UpdateJobDrag(int categoryIndex, JobEntry[] entries, float rowHeight, float scale)
    {
        if (jobDragCategoryIndex != categoryIndex || jobDragIndex < 0 || jobDragIndex >= entries.Length)
        {
            return;
        }

        if (!jobDragActive && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
            Vector2.Distance(ImGui.GetMousePos(), jobDragStart) > DragThreshold * scale)
        {
            jobDragActive = true;
        }

        if (!jobDragActive)
        {
            return;
        }

        var travel = ImGui.GetMousePos().Y - jobDragStart.Y;
        jobDragOffset = Math.Clamp(travel, -jobDragIndex * rowHeight,
            (entries.Length - 1 - jobDragIndex) * rowHeight);

        var targetIndex = Math.Clamp(jobDragIndex + (int)MathF.Round(jobDragOffset / rowHeight), 0,
            entries.Length - 1);
        jobDragFromGearsetId = entries[jobDragIndex].GearsetId;
        jobDragToGearsetId = entries[targetIndex].GearsetId;
    }

    // Runs unconditionally every frame, not from the section card: if the dragged category stops
    // drawing mid-gesture (logout, the app closing, the category being deleted) nothing else would
    // ever clear jobDragIndex, and the handles would refuse every later drag.
    private void FinishJobDrag()
    {
        if (jobDragIndex < 0 || ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            return;
        }

        if (jobDragActive)
        {
            ReorderGearsetInCategory(jobDragCategoryIndex, jobDragFromGearsetId, jobDragToGearsetId);
        }

        ResetJobDrag();
    }

    private void ResetJobDrag()
    {
        jobDragCategoryIndex = -1;
        jobDragIndex = -1;
        jobDragFromGearsetId = -1;
        jobDragToGearsetId = -1;
        jobDragOffset = 0f;
        jobDragActive = false;
    }

    // Matched by gearset id rather than row index so a rebuild landing mid-drag cannot desync the
    // gesture from the list it was started against.
    private void ReorderGearsetInCategory(int categoryIndex, int fromGearsetId, int toGearsetId)
    {
        if (characterWatch.CurrentContentId == 0 || fromGearsetId < 0 || toGearsetId < 0)
        {
            return;
        }

        var categories = CategoriesForWrite();
        if (categoryIndex < 0 || categoryIndex >= categories.Count)
        {
            return;
        }

        var gearsetIds = categories[categoryIndex].GearsetIds;
        var fromIndex = gearsetIds.IndexOf(fromGearsetId);
        var toIndex = gearsetIds.IndexOf(toGearsetId);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            return;
        }

        gearsetIds.RemoveAt(fromIndex);
        gearsetIds.Insert(toIndex, fromGearsetId);
        configuration.Save();
        Rebuild();
    }

    private void DrawRowMenu(Rect content, PhoneTheme theme)
    {
        if (!menu.IsOpenFor(RowMenuId) || menuGearsetId < 0)
        {
            return;
        }

        var categories = CurrentCategories();
        var assignedIndex = -1;
        for (var index = 0; index < categories.Count; index++)
        {
            if (categories[index].GearsetIds.Contains(menuGearsetId))
            {
                assignedIndex = index;
                break;
            }
        }

        var removeIndex = assignedIndex >= 0 ? categories.Count : -1;
        var newIndex = categories.Count + (assignedIndex >= 0 ? 1 : 0);
        var items = new DropdownMenu.Item[newIndex + 1];
        for (var index = 0; index < categories.Count; index++)
        {
            items[index] = new DropdownMenu.Item(categories[index].Name, Selected: index == assignedIndex);
        }

        if (removeIndex >= 0)
        {
            items[removeIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.RemoveFromCategory),
                Glyph: FontAwesomeIcon.FolderMinus.ToIconString());
        }

        items[newIndex] = new DropdownMenu.Item(Loc.T(L.Jobs.NewCategory),
            Glyph: FontAwesomeIcon.FolderPlus.ToIconString());

        var picked = menu.Draw(content, theme, items);
        if (picked < 0)
        {
            return;
        }

        if (picked == newIndex)
        {
            OpenCategoryEditor(-1, menuGearsetId);
            return;
        }

        if (picked == removeIndex)
        {
            RemoveGearsetFromCategory(menuGearsetId);
            return;
        }

        AssignGearsetToCategory(menuGearsetId, picked);
    }

    private void AssignGearsetToCategory(int gearsetId, int categoryIndex)
    {
        if (characterWatch.CurrentContentId == 0)
        {
            return;
        }

        var categories = CategoriesForWrite();
        if (categoryIndex < 0 || categoryIndex >= categories.Count)
        {
            return;
        }

        RemoveGearsetFromCategories(categories, gearsetId);
        categories[categoryIndex].GearsetIds.Add(gearsetId);
        configuration.Save();
        Rebuild();
    }

    private void RemoveGearsetFromCategory(int gearsetId)
    {
        if (characterWatch.CurrentContentId == 0)
        {
            return;
        }

        RemoveGearsetFromCategories(CategoriesForWrite(), gearsetId);
        configuration.Save();
        Rebuild();
    }

    private static void RemoveGearsetFromCategories(List<JobsCategory> categories, int gearsetId)
    {
        for (var index = 0; index < categories.Count; index++)
        {
            categories[index].GearsetIds.Remove(gearsetId);
        }
    }

    private void DeleteCategory(int index)
    {
        var categories = CurrentCategories();
        if (index < 0 || index >= categories.Count)
        {
            return;
        }

        var category = categories[index];
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Jobs.DeleteCategoryConfirm, category.Name),
            ConfirmLabel = Loc.T(L.Jobs.DeleteCategory),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            Confirm = () =>
            {
                CurrentCategories().Remove(category);
                configuration.Save();
                Rebuild();
            },
        });
    }

    private void OpenCategoryEditor(int categoryIndex, int gearsetId)
    {
        var categories = CurrentCategories();
        categoryEditorIndex = categoryIndex >= 0 && categoryIndex < categories.Count ? categoryIndex : -1;
        categoryEditorGearsetId = gearsetId;
        categoryEditorName = categoryEditorIndex >= 0 ? categories[categoryEditorIndex].Name : string.Empty;
        categoryEditorOpenedFrame = ImGui.GetFrameCount();
        focusCategoryField = true;
        categoryEditorOpen = true;
    }

    private void CloseCategoryEditor()
    {
        categoryEditorOpen = false;
        categoryEditorIndex = -1;
        categoryEditorGearsetId = -1;
    }

    private bool CategoryEditorClicked() =>
        categoryEditorOpenedFrame != ImGui.GetFrameCount() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);

    private void DrawCategoryEditor(Rect content, float scale)
    {
        var theme = ui.Theme;
        var pad = Metrics.Space.Md * scale;
        var gap = Metrics.Space.Md * scale;
        var width = EditorWidth * scale;
        var titleHeight = EditorTitleHeight * scale;
        var fieldHeight = EditorFieldHeight * scale;
        var height = pad * 2f + titleHeight + gap + fieldHeight + gap + fieldHeight;
        var min = new Vector2(content.Center.X - width * 0.5f, content.Min.Y + 96f * scale);
        var max = min + new Vector2(width, height);

        var titleTop = min.Y + pad;
        var fieldTop = titleTop + titleHeight + gap;
        var buttonTop = fieldTop + fieldHeight + gap;
        var nameRect = new Rect(new Vector2(min.X + pad, fieldTop), new Vector2(max.X - pad, fieldTop + fieldHeight));
        var saveRect = new Rect(new Vector2(max.X - pad - PickerButtonWidth * scale, buttonTop),
            new Vector2(max.X - pad, buttonTop + fieldHeight));

        DrawCategoryFieldHost(nameRect, scale);

        var drawList = ImGui.GetForegroundDrawList();
        var screen = SceneChrome.ScreenFrom(content, theme, scale);
        Material.Veil(drawList, screen.Min, screen.Max, PickerScrim, theme.ScreenRounding * scale);
        PopoverSurface.Draw(drawList, min, max, PickerRounding * scale, theme, scale);
        var title = Loc.T(categoryEditorIndex >= 0 ? L.Jobs.RenameCategory : L.Jobs.NewCategoryTitle);
        Typography.Draw(drawList, new Vector2(min.X + pad, titleTop), title, theme.TextStrong,
            TextStyles.SubheadlineEmphasized);

        DrawPickerField(drawList, nameRect, theme, scale);
        var named = categoryEditorName.Length > 0;
        var nameText = Typography.FitText(named ? categoryEditorName : Loc.T(L.Jobs.CategoryNamePlaceholder),
            nameRect.Width - Metrics.Space.Md * 2f * scale, TextStyles.Body);
        Typography.Draw(drawList, FieldTextOrigin(nameRect, nameText, TextStyles.Body, scale), nameText,
            named ? theme.TextStrong : theme.TextMuted, TextStyles.Body);

        DrawCategorySaveButton(drawList, saveRect, theme, scale);
        if (categoryEditorOpenedFrame == ImGui.GetFrameCount())
        {
            return;
        }

        var clickedOutside = UiInteract.ClickedOutside(min, max, false);
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || clickedOutside)
        {
            CloseCategoryEditor();
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Enter))
        {
            SaveCategoryEditor();
        }
    }

    private void DrawCategoryFieldHost(Rect nameRect, float scale)
    {
        var inset = Metrics.Space.Md * scale;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, default(Vector4))
                   .Push(ImGuiCol.FrameBgHovered, default(Vector4))
                   .Push(ImGuiCol.FrameBgActive, default(Vector4))
                   .Push(ImGuiCol.Text, default(Vector4))
                   .Push(ImGuiCol.Border, default(Vector4)))
        {
            ImGui.SetCursorScreenPos(new Vector2(nameRect.Min.X + inset, nameRect.Min.Y));
            using (ImRaii.Child("##jobsCategoryNameHost", new Vector2(nameRect.Width - inset, nameRect.Height), false,
                       HostFlags))
            {
                if (focusCategoryField)
                {
                    focusCategoryField = false;
                    ImGui.SetKeyboardFocusHere();
                }

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputText("##jobsCategoryNameField", ref categoryEditorName, CategoryNameMaxLength);
            }
        }
    }

    private void DrawCategorySaveButton(ImDrawListPtr drawList, Rect rect, PhoneTheme theme, float scale)
    {
        var enabled = categoryEditorName.Trim().Length > 0;
        var hovered = enabled && UiInteract.HoverWindowOnly(rect.Min, rect.Max);
        var fill = !enabled
            ? Palette.WithAlpha(theme.TextMuted, 0.2f)
            : hovered
                ? Palette.Mix(ui.Accent, PickerInkOnDark, 0.14f)
                : ui.Accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        var ink = enabled
            ? Palette.Luminance(fill) > 0.62f ? PickerInkOnLight : PickerInkOnDark
            : theme.TextMuted;
        Typography.DrawCentered(drawList, rect.Center, Loc.T(L.Jobs.SaveCategory), ink,
            TextStyles.SubheadlineEmphasized);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (CategoryEditorClicked())
        {
            SaveCategoryEditor();
        }
    }

    private void SaveCategoryEditor()
    {
        var name = categoryEditorName.Trim();
        if (name.Length == 0 || characterWatch.CurrentContentId == 0)
        {
            return;
        }

        var categories = CategoriesForWrite();
        if (categoryEditorIndex >= 0 && categoryEditorIndex < categories.Count)
        {
            categories[categoryEditorIndex].Name = name;
        }
        else
        {
            var category = new JobsCategory { Name = name };
            if (categoryEditorGearsetId >= 0)
            {
                RemoveGearsetFromCategories(categories, categoryEditorGearsetId);
                category.GearsetIds.Add(categoryEditorGearsetId);
            }

            categories.Add(category);
        }

        configuration.Save();
        CloseCategoryEditor();
        Rebuild();
    }
}
