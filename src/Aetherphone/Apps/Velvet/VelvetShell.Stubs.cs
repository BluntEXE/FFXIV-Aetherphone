using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private enum PostSheetAction
    {
        View,
        Audience,
        Delete,
        Report,
        Block,
    }

    private readonly ActionSheet postSheet = new();
    private readonly ActionSheet threadSheet = new();
    private readonly ActionSheet.Item[] postSheetItems = new ActionSheet.Item[3];
    private readonly PostSheetAction[] postSheetActions = new PostSheetAction[3];
    private readonly ActionSheet.Item[] threadSheetItems = new ActionSheet.Item[1];
    private int postSheetCount;
    private bool sheetPostInFeed;
    private string postSheetTitle = string.Empty;
    private VelvetMessagesTab messagesTab = VelvetMessagesTab.Chats;
    private VelvetPostDto? sheetPost;
    private string? sheetThreadId;
}
