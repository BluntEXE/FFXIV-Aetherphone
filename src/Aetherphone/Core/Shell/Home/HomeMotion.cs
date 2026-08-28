namespace Aetherphone.Core.Shell.Home;

internal readonly struct HomeMotion
{
    public readonly float Zoom;
    public readonly Vector2 Pivot;
    public readonly float Progress;
    public readonly bool Interactive;
    public readonly string? RevealAppId;

    public HomeMotion(float zoom, Vector2 pivot, float progress, bool interactive, string? revealAppId = null)
    {
        Zoom = zoom;
        Pivot = pivot;
        Progress = progress;
        Interactive = interactive;
        RevealAppId = revealAppId;
    }

    public static HomeMotion Rest => new(1f, default, 0f, true);

    public static HomeMotion Still => new(1f, default, 0f, false);

    public static HomeMotion Recede(float progress, string? revealAppId) => new(1f, default, progress, false, revealAppId);

    public Vector2 Warp(Vector2 point) => Pivot + (point - Pivot) * Zoom;

    public Rect Warp(Rect rect) => new(Warp(rect.Min), Warp(rect.Max));

    public bool Reveals(string appId) => RevealAppId is not null && string.Equals(RevealAppId, appId, StringComparison.Ordinal);
}
