using Aetherphone.Core;

namespace Aetherphone.Windows;

internal static class PopoutPlacement
{
    public static bool AnchorsBottomEdge(in Rect frame, in Rect viewport) =>
        frame.Center.Y > viewport.Center.Y;

    public static Vector2 AnchoredPosition(in Rect frame, bool anchorsBottomEdge, float scaledHeight) =>
        anchorsBottomEdge
            ? new Vector2(frame.Min.X, frame.Max.Y - scaledHeight)
            : frame.Min;
}
