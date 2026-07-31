using Aetherphone.Core.Aethernet;

namespace Aetherphone.Core.Moderation;

internal sealed class SuspensionGate
{
    private static readonly string[] SocialAppIds =
    {
        "message", "chirper", "aethergram", "velvet", "muster", "yellowpages",
    };

    private readonly AethernetSession session;

    public SuspensionGate(AethernetSession session)
    {
        this.session = session;
    }

    public event Action? Blocked;

    public bool Blocks(string appId)
    {
        if (!session.IsBanned)
        {
            return false;
        }

        for (var index = 0; index < SocialAppIds.Length; index++)
        {
            if (string.Equals(appId, SocialAppIds[index], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void ReportBlocked()
    {
        Blocked?.Invoke();
    }
}
