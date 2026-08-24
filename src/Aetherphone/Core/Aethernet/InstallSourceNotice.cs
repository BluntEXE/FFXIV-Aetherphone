using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet;

internal static class InstallSourceNotice
{
    public static void Poll(AethernetSession session, ConfirmService confirm)
    {
        var notice = session.ConsumeSourceNotice();
        if (notice is null)
        {
            return;
        }

        var blocked = string.Equals(notice, AethernetClientIdentity.StatusBlocked, StringComparison.Ordinal);
        var title = Loc.T(blocked ? L.Account.FailSourceBlockedTitle : L.Account.SourceWarnedTitle);
        var body = Loc.T(blocked ? L.Account.FailSourceBlockedBody : L.Account.SourceWarnedBody,
            AepConstants.OfficialRepositoryUrl);
        confirm.Alert(title, body, Loc.T(L.Account.FailDismiss));
    }
}
