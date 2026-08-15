namespace Aetherphone.Core.Video;

internal sealed class AetherStreamLauncher
{
    private bool upNextPending;

    public void RequestUpNext() => upNextPending = true;

    public bool TryConsumeUpNext()
    {
        if (!upNextPending)
        {
            return false;
        }

        upNextPending = false;
        return true;
    }
}
