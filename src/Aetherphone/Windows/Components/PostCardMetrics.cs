namespace Aetherphone.Windows.Components;

/// <summary>
/// Sizing tokens shared by every social feed post card (Aethergram, Velvet) so the cards stay
/// identical in padding, media rhythm and action button size. Unscaled pixels: multiply by
/// <c>ImGuiHelpers.GlobalScale</c> at the call site.
/// </summary>
internal static class PostCardMetrics
{
    public const float Pad = 14f;
    public const float Rounding = 18f;
    public const float MediaRounding = 14f;
    public const float HeaderBlock = 40f;
    public const float AvatarRadius = 18f;
    public const float NameGap = 12f;
    public const float SublineTop = 21f;
    public const float MediaGap = 12f;
    public const float ActionsGap = 12f;
    public const float ActionsHeight = 24f;
    public const float ActionIconRadius = 15f;
    public const float ActionIconInset = 13f;
    public const float ActionCountGap = 20f;
    public const float TextGap = 10f;
    public const float CaptionGap = 6f;
    public const float CardGap = 12f;
}
