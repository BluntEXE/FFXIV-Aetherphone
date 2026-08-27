using Dalamud.Game.Config;

namespace Aetherphone.Core.Notifications;

internal sealed class UiSoundService : IDisposable
{
    private const long GameVolumeRefreshMilliseconds = 1000;

    private readonly Configuration configuration;
    private readonly UiSoundPlayer player;
    private readonly long[] lastPlayed;
    private readonly int[] variantCursor;
    private float gameScale = 1f;
    private long gameScaleRefreshed;

    public UiSoundService(Configuration configuration, UiSoundPlayer player)
    {
        this.configuration = configuration;
        this.player = player;
        lastPlayed = new long[UiSoundCatalog.Entries.Length];
        variantCursor = new int[UiSoundCatalog.Entries.Length];
    }

    public void Play(UiSound sound)
    {
        if (configuration.SilentMode || !configuration.UiSounds)
        {
            return;
        }

        var index = (int)sound;
        ref readonly var entry = ref UiSoundCatalog.Entries[index];
        if (!ChannelEnabled(entry.Channel))
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now - lastPlayed[index] < entry.MinimumIntervalMilliseconds)
        {
            return;
        }

        var baseVolume = entry.Channel == UiSoundChannel.Game
            ? configuration.GameSoundVolume
            : configuration.UiSoundVolume;
        var volume = entry.Gain * baseVolume * gameScale;
        if (volume <= 0f)
        {
            return;
        }

        lastPlayed[index] = now;
        var files = entry.Files;
        var cursor = variantCursor[index];
        variantCursor[index] = (cursor + 1) % files.Length;
        player.Play(files[cursor], volume);
    }

    public void Maintain()
    {
        var now = Environment.TickCount64;
        if (now - gameScaleRefreshed >= GameVolumeRefreshMilliseconds)
        {
            gameScaleRefreshed = now;
            gameScale = ReadGameScale();
        }

        player.CloseIfIdle();
    }

    private bool ChannelEnabled(UiSoundChannel channel) => channel switch
    {
        UiSoundChannel.Transition => configuration.UiSoundTransitions,
        UiSoundChannel.Tap => configuration.UiSoundTaps,
        UiSoundChannel.Toggle => configuration.UiSoundToggles,
        UiSoundChannel.Keyboard => configuration.UiSoundKeyboard,
        UiSoundChannel.Game => configuration.GameSounds,
        _ => true,
    };

    private static float ReadGameScale()
    {
        try
        {
            var gameConfig = Plugin.GameConfig;
            if (gameConfig.TryGet(SystemConfigOption.IsSndMaster, out uint masterMuted) && masterMuted != 0)
            {
                return 0f;
            }

            if (gameConfig.TryGet(SystemConfigOption.IsSndSystem, out uint systemMuted) && systemMuted != 0)
            {
                return 0f;
            }

            var scale = 1f;
            if (gameConfig.TryGet(SystemConfigOption.SoundMaster, out uint master))
            {
                scale *= master / 100f;
            }

            if (gameConfig.TryGet(SystemConfigOption.SoundSystem, out uint system))
            {
                scale *= system / 100f;
            }

            return Math.Clamp(scale, 0f, 1f);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[UiSound] reading the game volume failed");
            return 1f;
        }
    }

    public void Dispose() => player.Dispose();
}

internal static class UiFeedback
{
    private static UiSoundService? service;

    public static void Bind(UiSoundService bound) => service = bound;

    public static void Unbind() => service = null;

    public static void Play(UiSound sound) => service?.Play(sound);
}
