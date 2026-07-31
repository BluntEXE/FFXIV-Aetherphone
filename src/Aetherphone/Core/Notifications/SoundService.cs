using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Notifications;

internal sealed class SoundService : IDisposable
{
    private readonly Configuration configuration;
    private readonly SoundLibrary library;
    private readonly SoundEffectPlayer player;

    public SoundService(Configuration configuration, SoundLibrary library, SoundEffectPlayer player)
    {
        this.configuration = configuration;
        this.library = library;
        this.player = player;
    }

    public IReadOnlyList<string> Options => library.Options;

    public string Resolve(string? token) => library.Resolve(token);

    public string Label(string? token)
    {
        var resolved = library.Resolve(token);
        return SoundTokens.TryFile(resolved, out var fileName)
            ? SoundLibrary.PrettyFileName(fileName)
            : Loc.T(L.Catalogs.RingtoneSilent);
    }

    public void Preview(string? token, float volume)
    {
        player.StopOneShots();
        Play(token, volume);
    }

    public void StopPreview() => player.StopOneShots();

    public string AddUserFile(string sourcePath) => library.AddUserFile(sourcePath);

    public void PlayNotification(string appId) =>
        Play(configuration.ResolveNotificationToken(appId), configuration.NotificationVolume);

    public void StartCallRing()
    {
        if (TryResolvePath(configuration.RingtoneSound, out var path))
        {
            player.PlayLoop(path, configuration.RingtoneVolume);
        }
    }

    public void StopCallRing() => player.StopLoop();

    private void Play(string? token, float volume)
    {
        if (TryResolvePath(token, out var path))
        {
            player.PlayOnce(path, volume);
        }
    }

    private bool TryResolvePath(string? token, out string path)
    {
        var resolved = library.Resolve(token);
        if (SoundTokens.TryFile(resolved, out var fileName))
        {
            return library.TryResolvePath(fileName, out path);
        }

        path = string.Empty;
        return false;
    }

    public void Dispose() => player.Dispose();
}
