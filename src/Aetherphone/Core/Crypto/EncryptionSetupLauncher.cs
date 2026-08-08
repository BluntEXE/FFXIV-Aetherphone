namespace Aetherphone.Core.Crypto;

internal sealed class EncryptionSetupLauncher
{
    private volatile bool pending;

    public void Request()
    {
        pending = true;
    }

    public bool TryConsume()
    {
        if (!pending)
        {
            return false;
        }

        pending = false;
        return true;
    }
}
