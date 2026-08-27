namespace Aetherphone.Core.Notifications;

internal enum UiSound
{
    Wake,
    Sleep,
    AppOpen,
    AppClose,
    Shutter,
    MessageSent,
    Success,
    Payout,
    Caution,
    Blocked,
    Tap,
    ToggleOn,
    ToggleOff,
    Keystroke,
    CallConnect,
    CallEnd,
    RecordStart,
    RecordCancel,
    GameWin,
    Refresh,
    GameHitSoft,
    GameHitWood,
    GameBreak,
    GameExplosion,
    GamePop,
    GameCollect,
    GameMatch,
    GameClear,
    GamePowerUp,
    GameShoot,
    GameJump,
    GameCardPlace,
    GameCardFlip,
    GameShuffle,
    GamePiece,
    GameTick,
    GameWrong,
    SimonTone1,
    SimonTone2,
    SimonTone3,
    SimonTone4,
}

internal enum UiSoundChannel
{
    Event,
    Transition,
    Tap,
    Toggle,
    Keyboard,
    Game,
}

internal readonly struct UiSoundEntry
{
    public readonly string[] Files;
    public readonly float Gain;
    public readonly int MinimumIntervalMilliseconds;
    public readonly UiSoundChannel Channel;

    public UiSoundEntry(string[] files, float gain, int minimumIntervalMilliseconds, UiSoundChannel channel)
    {
        Files = files;
        Gain = gain;
        MinimumIntervalMilliseconds = minimumIntervalMilliseconds;
        Channel = channel;
    }
}

internal static class UiSoundCatalog
{
    private static readonly string[] TransitionUp = { "Ui/transition_up.wav" };
    private static readonly string[] TransitionDown = { "Ui/transition_down.wav" };
    private static readonly string[] Shutter = { "Ui/shutter.wav" };
    private static readonly string[] Send = { "Ui/send.wav" };
    private static readonly string[] Success = { "Ui/success.wav" };
    private static readonly string[] Coin = { "Ui/coin.wav" };
    private static readonly string[] Caution = { "Ui/caution.wav" };
    private static readonly string[] Blocked = { "Ui/blocked.wav" };
    private static readonly string[] ToggleOn = { "Ui/toggle_on.wav" };
    private static readonly string[] ToggleOff = { "Ui/toggle_off.wav" };
    private static readonly string[] Button = { "Ui/button.wav" };
    private static readonly string[] Swipe = { "Ui/swipe.wav" };

    private static readonly string[] Taps =
    {
        "Ui/tap_1.wav", "Ui/tap_2.wav", "Ui/tap_3.wav", "Ui/tap_4.wav", "Ui/tap_5.wav",
    };

    private static readonly string[] Keystrokes =
    {
        "Ui/type_1.wav", "Ui/type_2.wav", "Ui/type_3.wav", "Ui/type_4.wav", "Ui/type_5.wav",
    };

    private static readonly string[] HitSoft =
    {
        "Games/hit_soft_1.wav", "Games/hit_soft_2.wav", "Games/hit_soft_3.wav",
    };

    private static readonly string[] HitWood =
    {
        "Games/hit_wood_1.wav", "Games/hit_wood_2.wav", "Games/hit_wood_3.wav",
    };

    private static readonly string[] Break =
    {
        "Games/break_1.wav", "Games/break_2.wav", "Games/break_3.wav",
    };

    private static readonly string[] Explosion = { "Games/explosion_1.wav", "Games/explosion_2.wav" };

    private static readonly string[] Pop =
    {
        "Games/pop_1.wav", "Games/pop_2.wav", "Games/pop_3.wav",
    };

    private static readonly string[] Collect = { "Games/collect_1.wav", "Games/collect_2.wav" };
    private static readonly string[] Match = { "Games/match_1.wav", "Games/match_2.wav" };
    private static readonly string[] Clear = { "Games/clear_1.wav", "Games/clear_2.wav" };
    private static readonly string[] PowerUp = { "Games/powerup_1.wav", "Games/powerup_2.wav" };
    private static readonly string[] Shoot = { "Games/shoot_1.wav", "Games/shoot_2.wav" };

    private static readonly string[] Jump =
    {
        "Games/jump_1.wav", "Games/jump_2.wav", "Games/jump_3.wav",
    };

    private static readonly string[] CardPlace =
    {
        "Games/card_place_1.wav", "Games/card_place_2.wav", "Games/card_place_3.wav",
    };

    private static readonly string[] CardFlip =
    {
        "Games/card_flip_1.wav", "Games/card_flip_2.wav", "Games/card_flip_3.wav",
    };

    private static readonly string[] Shuffle = { "Games/shuffle.wav" };

    private static readonly string[] Piece =
    {
        "Games/piece_1.wav", "Games/piece_2.wav", "Games/piece_3.wav",
    };

    private static readonly string[] Tick = { "Games/tick_1.wav", "Games/tick_2.wav" };
    private static readonly string[] Wrong = { "Games/wrong_1.wav", "Games/wrong_2.wav" };
    private static readonly string[] Simon1 = { "Games/simon_1.wav" };
    private static readonly string[] Simon2 = { "Games/simon_2.wav" };
    private static readonly string[] Simon3 = { "Games/simon_3.wav" };
    private static readonly string[] Simon4 = { "Games/simon_4.wav" };

    public static readonly UiSoundEntry[] Entries =
    {
        new(TransitionUp, 0.9f, 120, UiSoundChannel.Event),
        new(TransitionDown, 0.9f, 120, UiSoundChannel.Event),
        new(TransitionUp, 0.55f, 90, UiSoundChannel.Transition),
        new(TransitionDown, 0.55f, 90, UiSoundChannel.Transition),
        new(Shutter, 1f, 150, UiSoundChannel.Event),
        new(Send, 0.8f, 60, UiSoundChannel.Event),
        new(Success, 0.75f, 400, UiSoundChannel.Event),
        new(Coin, 0.7f, 120, UiSoundChannel.Event),
        new(Caution, 0.8f, 200, UiSoundChannel.Event),
        new(Blocked, 0.7f, 200, UiSoundChannel.Event),
        new(Taps, 0.7f, 35, UiSoundChannel.Tap),
        new(ToggleOn, 0.7f, 40, UiSoundChannel.Toggle),
        new(ToggleOff, 0.7f, 40, UiSoundChannel.Toggle),
        new(Keystrokes, 0.6f, 25, UiSoundChannel.Keyboard),
        new(Button, 0.8f, 400, UiSoundChannel.Event),
        new(TransitionDown, 0.75f, 400, UiSoundChannel.Event),
        new(ToggleOn, 0.75f, 150, UiSoundChannel.Event),
        new(ToggleOff, 0.75f, 150, UiSoundChannel.Event),
        new(Success, 0.65f, 800, UiSoundChannel.Event),
        new(Swipe, 0.55f, 250, UiSoundChannel.Event),
        new(HitSoft, 0.6f, 40, UiSoundChannel.Game),
        new(HitWood, 0.55f, 40, UiSoundChannel.Game),
        new(Break, 0.55f, 45, UiSoundChannel.Game),
        new(Explosion, 0.65f, 90, UiSoundChannel.Game),
        new(Pop, 0.55f, 35, UiSoundChannel.Game),
        new(Collect, 0.5f, 50, UiSoundChannel.Game),
        new(Match, 0.55f, 60, UiSoundChannel.Game),
        new(Clear, 0.6f, 150, UiSoundChannel.Game),
        new(PowerUp, 0.6f, 200, UiSoundChannel.Game),
        new(Shoot, 0.45f, 60, UiSoundChannel.Game),
        new(Jump, 0.5f, 60, UiSoundChannel.Game),
        new(CardPlace, 0.6f, 50, UiSoundChannel.Game),
        new(CardFlip, 0.6f, 50, UiSoundChannel.Game),
        new(Shuffle, 0.6f, 300, UiSoundChannel.Game),
        new(Piece, 0.6f, 40, UiSoundChannel.Game),
        new(Tick, 0.55f, 35, UiSoundChannel.Game),
        new(Wrong, 0.6f, 150, UiSoundChannel.Game),
        new(Simon1, 0.55f, 1, UiSoundChannel.Game),
        new(Simon2, 0.55f, 1, UiSoundChannel.Game),
        new(Simon3, 0.55f, 1, UiSoundChannel.Game),
        new(Simon4, 0.55f, 1, UiSoundChannel.Game),
    };

    public static IReadOnlyList<string> Files()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var entryIndex = 0; entryIndex < Entries.Length; entryIndex++)
        {
            var files = Entries[entryIndex].Files;
            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                names.Add(files[fileIndex]);
            }
        }

        var built = new string[names.Count];
        names.CopyTo(built);
        return built;
    }
}
