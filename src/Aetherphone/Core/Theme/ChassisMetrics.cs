namespace Aetherphone.Core.Theme;

internal readonly struct ChassisMetrics
{
    private const float RailFraction = 0.0195f;
    private const float MetalFraction = 0.0115f;
    private const float GlassFraction = 0.0135f;
    private const float ScreenRoundingFraction = 0.1028f;

    public readonly float RailWidth;
    public readonly float MetalWidth;
    public readonly float GlassWidth;
    public readonly float DeviceRounding;

    private ChassisMetrics(float railWidth, float metalWidth, float glassWidth, float deviceRounding)
    {
        RailWidth = railWidth;
        MetalWidth = metalWidth;
        GlassWidth = glassWidth;
        DeviceRounding = deviceRounding;
    }

    public static ChassisMetrics For(float deviceWidth)
    {
        var metal = MetalFraction * deviceWidth;
        var glass = GlassFraction * deviceWidth;
        return new ChassisMetrics(RailFraction * deviceWidth, metal, glass,
            ScreenRoundingFraction * deviceWidth + metal + glass);
    }

    public static ChassisMetrics Default => For(PhoneSizeCatalog.SizeFor(PhoneSizeCatalog.DefaultScale).X);
}
