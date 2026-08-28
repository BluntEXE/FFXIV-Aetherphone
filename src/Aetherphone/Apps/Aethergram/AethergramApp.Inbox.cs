using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{

    private readonly ActionSheet.Item[] inboxRowSheetItems = new ActionSheet.Item[1];
    private string? inboxSheetThreadId;
    private string inboxSheetTitle = string.Empty;
    private int inboxTab;
    private readonly string[] inboxSegmentLabels = new string[2];

    private void DrawInbox(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Aethergram.InboxTitle), back);
        var scale = UiScale.Current;
        if (!dmStore.ThreadsLoaded && !dmStore.LoadingThreads)
        {
            dmStore.RefreshThreads();
        }

        var pad = 16f * scale;
        var segTop = area.Min.Y + AppHeader.Height * scale + 6f * scale;
        var segRect = new Rect(new Vector2(area.Min.X + pad, segTop),
            new Vector2(area.Max.X - pad, segTop + 32f * scale));
        var requestCount = dmStore.RequestCount;
        var requestsLabel = requestCount > 0
            ? Loc.T(L.Aethergram.RequestsCount, requestCount)
            : Loc.T(L.Aethergram.Requests);
        inboxSegmentLabels[0] = Loc.T(L.Aethergram.ChatsTab);
        inboxSegmentLabels[1] = requestsLabel;
        inboxTab = SegmentStrip.Draw("aethergram.inbox", segRect, inboxSegmentLabels, inboxTab,
            AppPalettes.Aethergram);
        var listRect = new Rect(new Vector2(area.Min.X, segRect.Max.Y + 8f * scale), area.Max);
        var showRequests = inboxTab == 1;
        var threads = dmStore.Threads;
        var visibleCount = 0;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].Pending == showRequests)
            {
                visibleCount++;
            }
        }

        using (AppSurface.BeginEdgeToEdge(listRect))
        {
            if (visibleCount == 0)
            {
                DrawInboxEmptyState(listRect, showRequests, threads.Length, scale);
            }
            else
            {
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                for (var index = 0; index < threads.Length; index++)
                {
                    if (threads[index].Pending == showRequests)
                    {
                        DrawInboxRow(threads[index]);
                    }
                }

                if (dmStore.LoadingMoreThreads)
                {
                    InfiniteScroll.DrawLoadingRow(listRect.Center.X, AppPalettes.Aethergram.MutedInk);
                }
                else if (dmStore.HasMoreThreads && InfiniteScroll.ReachedBottom())
                {
                    dmStore.LoadMoreThreads();
                }

                ImGui.Dummy(new Vector2(0f, 24f * scale));
            }
        }
    }

    private void DrawInboxEmptyState(Rect listRect, bool showRequests, int totalThreads, float scale)
    {
        if (dmStore.LoadingThreads && totalThreads == 0)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
                Loc.T(L.Common.Loading), AppPalettes.Aethergram.MutedInk);
            return;
        }

        if (showRequests)
        {
            Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
                Loc.T(L.Aethergram.RequestsEmpty), AppPalettes.Aethergram.TitleInk, TextStyles.Headline);
            return;
        }

        Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 80f * scale),
            Loc.T(L.Aethergram.InboxEmpty), AppPalettes.Aethergram.TitleInk, TextStyles.Headline);
        Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 106f * scale),
            Loc.T(L.Aethergram.InboxEmptyHint), AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline);
    }

    private void DrawInboxRow(GramThreadDto thread)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var rowHeight = 64f * scale;
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var origin = cell.Bounds.Min;
        var width = cell.Bounds.Width;
        var pad = FeedCell.PadX * scale;
        var avatarRadius = 22f * scale;
        var avatarCenter = new Vector2(origin.X + pad + avatarRadius, origin.Y + rowHeight * 0.5f);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent,
            Monogram(thread.OtherDisplayName, thread.OtherHandle), 0.95f,
            images.Avatar(thread.OtherAvatarUrl, avatarRadius * 2f), 32);
        PresenceDot(drawList, new Vector2(avatarCenter.X + avatarRadius - 4f * scale,
            avatarCenter.Y + avatarRadius - 4f * scale), thread.Presence);
        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var textRight = origin.X + width - pad;
        var timeText = thread.LastMessageAtUnix > 0 ? TimeText.Short(thread.LastMessageAtUnix) : string.Empty;
        var timeSize = timeText.Length > 0 ? Typography.Measure(timeText, TextStyles.Footnote) : Vector2.Zero;
        var title = SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle);
        var titleWidth = textRight - textLeft - (timeSize.X > 0f ? timeSize.X + 8f * scale : 0f);
        Typography.Draw(new Vector2(textLeft, origin.Y + 12f * scale),
            Typography.FitText(title, titleWidth, 1f, FontWeight.SemiBold), theme.TextStrong, 1f,
            FontWeight.SemiBold);
        if (timeText.Length > 0)
        {
            Typography.Draw(new Vector2(textRight - timeSize.X, origin.Y + 13f * scale), timeText,
                AppPalettes.Aethergram.MutedInk, TextStyles.Footnote);
        }

        var unread = thread.UnreadCount;
        var preview = string.IsNullOrEmpty(thread.LastMessagePreview)
            ? Loc.T(L.Aethergram.ThreadEmpty)
            : ChatText.ListPreview(thread.LastMessagePreview);
        var previewWidth = textRight - textLeft - (unread > 0 ? 22f * scale : 0f);
        Typography.Draw(new Vector2(textLeft, origin.Y + 35f * scale),
            Typography.FitText(preview, previewWidth, TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight),
            unread > 0 ? AppPalettes.Aethergram.BodyInk : AppPalettes.Aethergram.MutedInk, TextStyles.Subheadline);
        if (unread > 0)
        {
            ActivityBadge.Draw(new Vector2(textRight - 7f * scale, origin.Y + 42f * scale), unread, theme, scale);
        }

        if (cell.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenInboxRowSheet(thread);
        }
        else if (cell.Tapped)
        {
            OpenThread(thread.OtherUserId);
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void OpenInboxRowSheet(GramThreadDto thread)
    {
        inboxSheetThreadId = thread.OtherUserId;
        inboxSheetTitle = SocialIdentity.Name(thread.OtherDisplayName, thread.OtherHandle);
        inboxRowSheetItems[0] = new ActionSheet.Item(Loc.T(L.Aethergram.DeleteConversation), string.Empty, true);
        inboxRowSheet.Open();
    }

    private void DrawInboxRowSheet(Rect screen)
    {
        if (!inboxRowSheet.CapturesPointer)
        {
            return;
        }

        if (inboxRowSheet.IsOpen && router.Current.Screen != AethergramScreen.Inbox)
        {
            inboxRowSheet.Close();
        }

        var picked = inboxRowSheet.Draw(screen, ActionSheetStyle.From(ui), inboxRowSheetItems,
            Loc.T(L.Common.Cancel), false, inboxSheetTitle);
        if (picked == 0 && inboxSheetThreadId is { } otherId)
        {
            AskDeleteConversation(otherId);
        }
    }

    private void AskDeleteConversation(string otherId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Aethergram.DeleteConversation),
            Message = Loc.T(L.Aethergram.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.Aethergram.DeleteConfirm),
            CancelLabel = Loc.T(L.Aethergram.DeleteCancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(otherId),
        });
    }

    private void DeleteConversation(string otherId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == AethergramScreen.Thread && current.Id == otherId;
        dmStore.DeleteThread(otherId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private void OpenInbox()
    {
        router.Push(AethergramRoute.Inbox);
    }

    private void OpenThread(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        router.Push(AethergramRoute.Thread(userId));
    }
}
