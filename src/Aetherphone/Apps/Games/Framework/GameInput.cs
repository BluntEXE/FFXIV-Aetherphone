using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameInput
{
    private static readonly VirtualKey[] ConsumedKeys =
    {
        VirtualKey.W, VirtualKey.A, VirtualKey.S, VirtualKey.D, VirtualKey.Z, VirtualKey.X, VirtualKey.C,
        VirtualKey.UP, VirtualKey.DOWN, VirtualKey.LEFT, VirtualKey.RIGHT, VirtualKey.SPACE,
        VirtualKey.BACK, VirtualKey.DELETE, VirtualKey.RETURN,
        VirtualKey.KEY_1, VirtualKey.KEY_2, VirtualKey.KEY_3, VirtualKey.KEY_4, VirtualKey.KEY_5,
        VirtualKey.KEY_6, VirtualKey.KEY_7, VirtualKey.KEY_8, VirtualKey.KEY_9,
        VirtualKey.NUMPAD1, VirtualKey.NUMPAD2, VirtualKey.NUMPAD3, VirtualKey.NUMPAD4, VirtualKey.NUMPAD5,
        VirtualKey.NUMPAD6, VirtualKey.NUMPAD7, VirtualKey.NUMPAD8, VirtualKey.NUMPAD9,
    };

    private static int claimedFrame = -1;

    public static bool Claim()
    {
        if (!GameFocus.Active)
        {
            return false;
        }

        var frame = ImGui.GetFrameCount();
        if (claimedFrame == frame)
        {
            return true;
        }

        claimedFrame = frame;
        ImGui.GetIO().WantTextInput = true;
        var keyState = Plugin.KeyState;
        for (var keyIndex = 0; keyIndex < ConsumedKeys.Length; keyIndex++)
        {
            keyState[ConsumedKeys[keyIndex]] = false;
        }

        return true;
    }

    public static bool Held(ImGuiKey key) => Claim() && ImGui.IsKeyDown(key);

    public static bool Held(ImGuiKey key, ImGuiKey alternate) =>
        Claim() && (ImGui.IsKeyDown(key) || ImGui.IsKeyDown(alternate));

    public static bool Pressed(ImGuiKey key, bool repeat = false) => Claim() && ImGui.IsKeyPressed(key, repeat);

    public static bool Pressed(ImGuiKey key, ImGuiKey alternate, bool repeat = false) =>
        Claim() && (ImGui.IsKeyPressed(key, repeat) || ImGui.IsKeyPressed(alternate, repeat));
}
