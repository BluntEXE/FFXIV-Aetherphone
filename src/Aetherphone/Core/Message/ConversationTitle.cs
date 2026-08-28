using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Message;

internal static class ConversationTitle
{
    public static string Of(ConversationDto item)
    {
        if (item.IsGroup)
        {
            return item.Title.Length > 0 ? item.Title : Loc.T(L.DirectMessages.GroupFallback);
        }

        return item.OtherDisplayName.Length > 0 ? item.OtherDisplayName : item.OtherHandle;
    }
}
