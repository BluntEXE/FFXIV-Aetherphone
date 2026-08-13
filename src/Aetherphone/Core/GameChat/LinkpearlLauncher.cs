namespace Aetherphone.Core.GameChat;

internal sealed class LinkpearlLauncher
{
    private string? pendingKey;

    public void Request(string conversationKey) => pendingKey = conversationKey;

    public bool TryConsume(out string conversationKey)
    {
        if (pendingKey is null)
        {
            conversationKey = string.Empty;
            return false;
        }

        conversationKey = pendingKey;
        pendingKey = null;
        return true;
    }
}
