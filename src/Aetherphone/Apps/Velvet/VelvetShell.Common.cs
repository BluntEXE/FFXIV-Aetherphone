using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private static Rect Reserve(float heightUnscaled)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + heightUnscaled * scale));
        ImGui.Dummy(new Vector2(width, heightUnscaled * scale));
        return rect;
    }

    private static Rect Inset(Rect rect, float inset) =>
        new(new Vector2(rect.Min.X + inset, rect.Min.Y), new Vector2(rect.Max.X - inset, rect.Max.Y));

    private static void Gap(float pixels)
    {
        ImGui.Dummy(new Vector2(0f, pixels * UiScale.Current));
    }

    private static Rect AnchorBox(Vector2 center, float half)
    {
        var offset = new Vector2(half, half);
        return new Rect(center - offset, center + offset);
    }

    private static void WrapText(string text, Vector4 color, in TextStyle style)
    {
        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Typography.Wrapped(text);
        }

        ImGui.PopTextWrapPos();
    }

    private void OpenReport(string targetType, string targetId, string title)
    {
        report.Open(new ReportPrompt
        {
            Title = title,
            Submit = (reason, done) => store.Report(targetType, targetId, reason, succeeded =>
            {
                if (succeeded)
                {
                    reportedTargets.Add(targetId);
                }

                done(succeeded);
            }),
        });
    }

    private bool AlreadyReported(string targetId) => reportedTargets.Contains(targetId);

    private void OpenPostSheet(VelvetPostDto post, bool inFeed)
    {
        sheetPost = post;
        sheetPostInFeed = inFeed;
        postSheetTitle = DisplayNameOf(post.OwnerDisplayName, post.OwnerHandle);
        postSheetCount = 0;
        if (inFeed)
        {
            AddPostSheetItem(PostSheetAction.View, Loc.T(L.Velvet.ViewPost), false);
        }

        if (store.Me is { } me && me.UserId == post.OwnerId)
        {
            AddPostSheetItem(PostSheetAction.Audience,
                Loc.T(post.Audience == VelvetPostAudience.Public ? L.Velvet.MakeConnections : L.Velvet.MakePublic),
                false);
            AddPostSheetItem(PostSheetAction.Delete, Loc.T(L.Velvet.DeleteConfirm), true);
        }
        else
        {
            AddPostSheetItem(PostSheetAction.Report, Loc.T(L.Velvet.Report), true);
            AddPostSheetItem(PostSheetAction.Block, Loc.T(L.Velvet.Block), true);
        }

        postSheet.Open();
    }

    private void AddPostSheetItem(PostSheetAction action, string label, bool danger)
    {
        postSheetActions[postSheetCount] = action;
        postSheetItems[postSheetCount] = new ActionSheet.Item(label, string.Empty, danger);
        postSheetCount++;
    }

    private void DrawPostSheet(Rect screen)
    {
        if (!postSheet.CapturesPointer)
        {
            return;
        }

        var picked = postSheet.Draw(screen, ActionSheetStyle.From(ui), postSheetItems.AsSpan(0, postSheetCount),
            Loc.T(L.Common.Cancel), false, postSheetTitle);
        if (picked < 0 || sheetPost is not { } post)
        {
            return;
        }

        switch (postSheetActions[picked])
        {
            case PostSheetAction.View:
                OpenPostDetail(post.Id);
                break;
            case PostSheetAction.Audience:
                store.SetPostAudience(post, post.Audience == VelvetPostAudience.Public
                    ? VelvetPostAudience.Connections
                    : VelvetPostAudience.Public);
                break;
            case PostSheetAction.Delete:
                AskDeletePost(post.Id, sheetPostInFeed ? null : back);
                break;
            case PostSheetAction.Report:
                OpenReport("velvet_post", post.Id, Loc.T(L.Velvet.ReportPost));
                break;
            case PostSheetAction.Block:
                AskBlock(post.OwnerId, DisplayNameOf(post.OwnerDisplayName, post.OwnerHandle));
                break;
        }
    }

    private void AskBlock(string userId, string displayName)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Social.BlockConfirmTitle, displayName),
            Message = Loc.T(L.Velvet.BlockConfirm),
            ConfirmLabel = Loc.T(L.Velvet.Block),
            CancelLabel = Loc.T(L.Velvet.DeleteCancel),
            Danger = true,
            ConfirmAsync = done => store.Block(userId, done, confirm.ReportFailure),
        });
    }
}
