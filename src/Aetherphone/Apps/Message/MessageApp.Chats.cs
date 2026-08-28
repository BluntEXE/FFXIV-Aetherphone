using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const byte ChatFilterAll = 0;
    private const byte ChatFilterDirect = 1;
    private const byte ChatFilterGroups = 2;

    private readonly ActionSheet chatSheet = new();
    private readonly ActionSheet.Item[] chatSheetItems = new ActionSheet.Item[4];
    private string? sheetConversationId;
    private string chatSheetTitle = string.Empty;
    private byte chatFilter = ChatFilterAll;

    private void DrawChatsTab(Rect area)
    {
        if (!session.IsSignedIn)
        {
            EmptyState.Draw(area, ui, FontAwesomeIcon.Comments, DisplayName, Loc.T(L.DirectMessages.SignInPrompt));
            return;
        }

        if (!store.ConversationsLoaded && !store.LoadingConversations)
        {
            store.RefreshConversations();
        }

        var scale = UiScale.Current;
        var searchHeight = 52f * scale;
        SearchField.Draw(new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + searchHeight)),
            "##messageFilter", Loc.T(L.Phone.FilterHint), ref filter, AppPalettes.Message);
        var chipsHeight = 38f * scale;
        DrawChatFilterChips(new Rect(new Vector2(area.Min.X, area.Min.Y + searchHeight),
            new Vector2(area.Max.X, area.Min.Y + searchHeight + chipsHeight)), scale);
        var listRect = new Rect(new Vector2(area.Min.X, area.Min.Y + searchHeight + chipsHeight), area.Max);
        DrawRecoveryNudge(ref listRect);
        var pinned = new List<ConversationDto>();
        var regular = new List<ConversationDto>();
        CollectChats(pinned, regular, archived: false);
        if (pinned.Count == 0 && regular.Count == 0)
        {
            if (filter.Trim().Length > 0 || chatFilter != ChatFilterAll)
            {
                EmptyState.Draw(listRect, ui, FontAwesomeIcon.Search, Loc.T(L.Phone.NoOneFound), string.Empty);
            }
            else if (store.ThreadListFailed)
            {
                threadListFailure.Set(store.ThreadListFailure);
                if (EmptyState.Draw(listRect, ui, FontAwesomeIcon.ExclamationTriangle,
                        Loc.T(L.Failure.CouldNotLoad), threadListFailure.Text(), Loc.T(L.Common.Retry)))
                {
                    store.RefreshConversations();
                }
            }
            else if (EmptyState.Draw(listRect, ui, FontAwesomeIcon.Comments, Loc.T(L.DirectMessages.Empty),
                         Loc.T(L.DirectMessages.EmptyHint), Loc.T(L.DirectMessages.NewMessage)))
            {
                selectedContacts.Clear();
                groupTitleDraft = string.Empty;
                filter = string.Empty;
                router.Push(MessageRoute.NewChat);
            }
        }
        else
        {
            using (AppSurface.BeginEdgeToEdge(listRect))
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < pinned.Count; index++)
                {
                    DrawConversationRow(pinned[index], scale, pinned: true);
                }

                for (var index = 0; index < regular.Count; index++)
                {
                    DrawConversationRow(regular[index], scale, pinned: false);
                }

                if (store.LoadingMoreThreads)
                {
                    InfiniteScroll.DrawLoadingRow(listRect.Center.X, AppPalettes.Message.MutedInk);
                }
                else if (store.HasMoreThreads && InfiniteScroll.ReachedBottom())
                {
                    store.LoadMoreThreads();
                }

                ImGui.Dummy(new Vector2(0f, 72f * scale));
            }
        }

        if (ComposeFab.Draw(listRect, "##messageNewFab", ui.Accent, IconGlyph.Of(FontAwesomeIcon.Pen),
                Loc.T(L.DirectMessages.NewMessage)))
        {
            selectedContacts.Clear();
            groupTitleDraft = string.Empty;
            filter = string.Empty;
            router.Push(MessageRoute.NewChat);
        }
    }

    private void DrawRecoveryNudge(ref Rect listRect)
    {
        if (recoveryNudgeDismissed || configuration.EncryptionRecoveryNudgeDismissed
            || store.VaultState != KeyVaultState.Unlocked || store.Vault.RecoveryConfigured)
        {
            return;
        }

        ChatHeaderControls.DrawPromptBanner(ui, ref listRect, Loc.T(L.Encryption.RecoveryNudgeBanner), ui.MutedInk,
            () =>
            {
                recoveryNudgeDismissed = true;
                encryptionSetup.Request();
                navigation.Open("settings");
            },
            () => recoveryNudgeDismissed = true);
    }

    private void DrawChatFilterChips(Rect area, float scale)
    {
        var cursorX = area.Min.X + 16f * scale;
        var centerY = area.Center.Y;
        var gap = 8f * scale;
        if (ui.FlowChip(ref cursorX, centerY, gap, Loc.T(L.Collections.FilterAll), chatFilter == ChatFilterAll))
        {
            chatFilter = ChatFilterAll;
        }

        if (ui.FlowChip(ref cursorX, centerY, gap, Loc.T(L.Message.FilterDirect), chatFilter == ChatFilterDirect))
        {
            chatFilter = ChatFilterDirect;
        }

        if (ui.FlowChip(ref cursorX, centerY, gap, Loc.T(L.Message.FilterGroups), chatFilter == ChatFilterGroups))
        {
            chatFilter = ChatFilterGroups;
        }
    }

    private void DrawArchived(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.Archived), back);
        var top = area.Min.Y + AppHeader.Height * scale;
        var listRect = new Rect(new Vector2(area.Min.X, top), area.Max);
        var pinned = new List<ConversationDto>();
        var archived = new List<ConversationDto>();
        CollectChats(pinned, archived, archived: true);
        if (archived.Count == 0)
        {
            EmptyState.Draw(listRect, ui, FontAwesomeIcon.BoxOpen, Loc.T(L.Message.NoArchived), string.Empty);
        }
        else
        {
            using (AppSurface.BeginEdgeToEdge(listRect))
            {
                ImGui.Dummy(new Vector2(0f, 4f * scale));
                for (var index = 0; index < archived.Count; index++)
                {
                    DrawConversationRow(archived[index], scale, pinned: false);
                }

                ImGui.Dummy(new Vector2(0f, 24f * scale));
            }
        }
    }

    private void CollectChats(List<ConversationDto> pinnedTarget, List<ConversationDto> regularTarget, bool archived)
    {
        var snapshot = store.Conversations;
        var query = filter.Trim();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var item = snapshot[index];
            if (configuration.MessageArchivedChats.Contains(item.Id) != archived)
            {
                continue;
            }

            if (chatFilter == ChatFilterDirect && item.IsGroup || chatFilter == ChatFilterGroups && !item.IsGroup)
            {
                continue;
            }

            if (query.Length > 0 && !DirectMessagesStore.DisplayTitle(item).Contains(query,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!archived && configuration.MessagePinnedChats.Contains(item.Id))
            {
                pinnedTarget.Add(item);
            }
            else
            {
                regularTarget.Add(item);
            }
        }
    }

    private void DrawConversationRow(ConversationDto item, float scale, bool pinned)
    {
        var rowHeight = 62f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var cell = FeedCell.Begin(drawList, rowHeight, ui.HoverWash);
        var origin = cell.Bounds.Min;
        var width = cell.Bounds.Width;
        var pad = FeedCell.PadX * scale;
        var radius = 22f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var title = DirectMessagesStore.DisplayTitle(item);
        if (item.IsGroup)
        {
            drawList.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.85f)), 32);
            AppSkin.Icon(avatarCenter, IconGlyph.Of(FontAwesomeIcon.Users), White, 1f);
        }
        else
        {
            AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, title, string.Empty, item.OtherAvatarUrl,
                images, lodestone, 0.95f, 32, 1f, Frames.Of(item.FrameId));
        }

        var timeLabel = ChatTime(item.LastMessageAtUnix);
        var timeSize = Typography.Measure(timeLabel, TextStyles.Caption1);
        Typography.Draw(new Vector2(origin.X + width - pad - timeSize.X, origin.Y + 12f * scale), timeLabel,
            ui.MutedInk, TextStyles.Caption1);
        var markerRight = origin.X + width - pad - timeSize.X;
        if (pinned)
        {
            AppSkin.Icon(new Vector2(markerRight - 12f * scale, origin.Y + 18f * scale),
                IconGlyph.Of(FontAwesomeIcon.Thumbtack), ui.MutedInk, 0.6f);
            markerRight -= 20f * scale;
        }

        if (item.Muted)
        {
            AppSkin.Icon(new Vector2(markerRight - 12f * scale, origin.Y + 18f * scale),
                IconGlyph.Of(FontAwesomeIcon.BellSlash), ui.MutedInk, 0.6f);
            markerRight -= 20f * scale;
        }

        if (!item.IsGroup && musters.ContactMusterFor(item.OtherUserId) is { } hosted)
        {
            var musterCenter = new Vector2(markerRight - 12f * scale, origin.Y + 18f * scale);
            var musterExtent = new Vector2(12f * scale, 12f * scale);
            var musterRect = new Rect(musterCenter - musterExtent, musterCenter + musterExtent);
            var overMuster = UiInteract.Hover(musterRect.Min, musterRect.Max);
            AppSkin.Icon(musterCenter, IconGlyph.Of(FontAwesomeIcon.Bullhorn), AppAccents.For(MusterStore.AppId),
                0.6f);
            HoverTooltip.Show(musterRect, Loc.T(L.Message.HostingMuster), HoverLabelSide.Above);
            if (UiInteract.Click(musterRect.Min, musterRect.Max, overMuster))
            {
                musterLauncher.RequestDetail(hosted.Id);
                navigation.Open(MusterStore.AppId);
            }

            markerRight -= 20f * scale;
        }

        var textLeft = avatarCenter.X + radius + 12f * scale;
        var textWidth = markerRight - 8f * scale - textLeft;
        var titleTop = origin.Y + 12f * scale;
        var titleSize = Typography.Measure(title, 1f, FontWeight.SemiBold);
        var titleHovering = UiInteract.Hover(new Vector2(textLeft, titleTop),
            new Vector2(textLeft + textWidth, titleTop + titleSize.Y));
        Marquee.DrawLeft("messageapp.chats.title." + item.Id, title, textLeft, titleTop, textWidth,
            new TextStyle(1f, FontWeight.SemiBold), theme.TextStrong, titleHovering);
        var previewColor = item.UnreadCount > 0 ? theme.TextStrong : ui.MutedInk;
        var previewRight = origin.X + width - (item.UnreadCount > 0 ? 40f * scale : pad);
        var draft = configuration.MessageDrafts.GetValueOrDefault(item.Id, string.Empty);
        if (draft.Length > 0)
        {
            var prefix = Loc.T(L.Message.DraftPrefix);
            var prefixSize = Typography.Measure(prefix, 0.85f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft, origin.Y + 33f * scale), prefix, ui.Accent, 0.85f,
                FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft + prefixSize.X + 4f * scale, origin.Y + 33f * scale),
                Typography.FitText(draft, previewRight - textLeft - prefixSize.X - 4f * scale, 0.85f,
                    FontWeight.Regular), ui.MutedInk, 0.85f);
        }
        else
        {
            var preview = item.LastMessagePreview.Length > 0
                ? ChatText.ListPreview(item.LastMessagePreview)
                : item.LastMessageKind switch
                {
                    1 => Loc.T(L.DirectMessages.PhotoPreview),
                    3 => Loc.T(L.DirectMessages.VoicePreview),
                    _ => string.Empty,
                };
            Typography.Draw(new Vector2(textLeft, origin.Y + 33f * scale),
                Typography.FitText(preview, previewRight - textLeft, 0.85f, FontWeight.Regular), previewColor, 0.85f);
        }
        if (item.UnreadCount > 0)
        {
            var badgeCenter = new Vector2(origin.X + width - 22f * scale, origin.Y + rowHeight - 20f * scale);
            drawList.AddCircleFilled(badgeCenter, 9f * scale, ImGui.GetColorU32(ui.Accent), 20);
            Typography.DrawCentered(badgeCenter, item.UnreadCount.ToString(Loc.Culture), White, 0.75f,
                FontWeight.SemiBold);
        }

        if (cell.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            OpenChatSheet(item);
        }
        else if (cell.Tapped)
        {
            router.Push(MessageRoute.Thread(item.Id));
        }

        FeedCell.End(drawList, cell, ui.Hairline);
    }

    private void OpenChatSheet(ConversationDto conversation)
    {
        var id = conversation.Id;
        sheetConversationId = id;
        chatSheetTitle = DirectMessagesStore.DisplayTitle(conversation);
        var isPinned = configuration.MessagePinnedChats.Contains(id);
        var isArchived = configuration.MessageArchivedChats.Contains(id);
        chatSheetItems[0] = new ActionSheet.Item(Loc.T(isPinned ? L.Common.Unpin : L.Common.Pin));
        chatSheetItems[1] = new ActionSheet.Item(Loc.T(isArchived ? L.Message.Unarchive : L.Message.Archive));
        chatSheetItems[2] = new ActionSheet.Item(Loc.T(conversation.Muted
            ? L.Message.UnmuteAction
            : L.Message.MuteAction));
        chatSheetItems[3] = new ActionSheet.Item(Loc.T(L.Message.DeleteConversation), string.Empty, true);
        chatSheet.Open();
    }

    private void DrawChatSheet(Rect screen)
    {
        if (!chatSheet.CapturesPointer)
        {
            return;
        }

        if (sheetConversationId is not { } id || FindConversationDto(id) is null)
        {
            chatSheet.Close();
        }

        var picked = chatSheet.Draw(screen, ActionSheetStyle.From(ui), chatSheetItems, Loc.T(L.Common.Cancel), false,
            chatSheetTitle);
        if (picked < 0 || sheetConversationId is not { } conversationId)
        {
            return;
        }

        if (picked == 0)
        {
            TogglePinned(conversationId);
        }
        else if (picked == 1)
        {
            ToggleArchived(conversationId);
        }
        else if (picked == 2)
        {
            store.SetMuted(conversationId, !(FindConversationDto(conversationId)?.Muted ?? false), _ => { });
        }
        else if (picked == 3)
        {
            AskDeleteConversation(conversationId);
        }
    }

    private void AskDeleteConversation(string conversationId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Message.DeleteConversation),
            Message = Loc.T(L.Message.DeleteConversationMessage),
            ConfirmLabel = Loc.T(L.Common.Delete),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => DeleteConversation(conversationId),
        });
    }

    private void DeleteConversation(string conversationId)
    {
        var current = router.Current;
        var threadOpen = current.Screen == MessageScreen.Thread && current.Id == conversationId;
        configuration.MessagePinnedChats.Remove(conversationId);
        configuration.MessageArchivedChats.Remove(conversationId);
        configuration.Save();
        store.DeleteThread(conversationId);
        if (threadOpen)
        {
            router.Pop();
        }
    }

    private ConversationDto? FindConversationDto(string id)
    {
        var snapshot = store.Conversations;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == id)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private void TogglePinned(string conversationId)
    {
        if (!configuration.MessagePinnedChats.Remove(conversationId))
        {
            configuration.MessagePinnedChats.Add(conversationId);
            configuration.MessageArchivedChats.Remove(conversationId);
        }

        configuration.Save();
    }

    private void ToggleArchived(string conversationId)
    {
        if (!configuration.MessageArchivedChats.Remove(conversationId))
        {
            configuration.MessageArchivedChats.Add(conversationId);
            configuration.MessagePinnedChats.Remove(conversationId);
        }

        configuration.Save();
    }

    private static string ChatTime(long unix)
    {
        if (unix <= 0)
        {
            return string.Empty;
        }

        var local = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        if (local.Date == today)
        {
            return TimeText.Clock(local);
        }

        return (today - local.Date).TotalDays < 7d
            ? local.ToString("ddd", Loc.Culture)
            : local.ToString("d", Loc.Culture);
    }
}
