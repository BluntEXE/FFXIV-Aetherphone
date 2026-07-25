namespace Aetherphone.Core.Video;

// Where the projected screen lives in the world - a stored position, yaw, and size, persisted in
// Configuration. Replaces the old Carbuncle/Penumbra render path: there is no game object here at
// all, just coordinates that ScreenPainter projects to screen space and draws against each frame.
internal sealed class ScreenPlacement
{
    private readonly Configuration configuration;

    public ScreenPlacement(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public bool IsPlaced => configuration.VideoScreenPlaced;

    public Vector3 Position => new(configuration.VideoScreenPositionX, configuration.VideoScreenPositionY,
        configuration.VideoScreenPositionZ);

    public float Yaw => configuration.VideoScreenYaw;
    public float Width => configuration.VideoScreenWidth;
    public float Height => configuration.VideoScreenHeight;

    public void PlaceAt(Vector3 position, float yaw)
    {
        configuration.VideoScreenPlaced = true;
        configuration.VideoScreenPositionX = position.X;
        configuration.VideoScreenPositionY = position.Y;
        configuration.VideoScreenPositionZ = position.Z;
        configuration.VideoScreenYaw = yaw;
        configuration.Save();
    }

    public void Clear()
    {
        configuration.VideoScreenPlaced = false;
        configuration.Save();
    }

    public void SetSize(float width, float height)
    {
        configuration.VideoScreenWidth = width;
        configuration.VideoScreenHeight = height;
        configuration.Save();
    }

    public void Nudge(Vector3 delta)
    {
        PlaceAt(Position + delta, Yaw);
    }

    public void Rotate(float deltaRadians)
    {
        PlaceAt(Position, Yaw + deltaRadians);
    }

    // Order matches AddImageQuad's expected UV winding (0,0 / 1,0 / 1,1 / 0,1): top-left,
    // top-right, bottom-right, bottom-left. The quad faces along "forward"; "right" is where the
    // screen extends horizontally, Y is where it extends vertically - no roll/pitch, just yaw,
    // which is all a wall- or ground-anchored screen needs.
    public (Vector3 TopLeft, Vector3 TopRight, Vector3 BottomRight, Vector3 BottomLeft) ComputeCorners()
    {
        var halfWidth = Width * 0.5f;
        var halfHeight = Height * 0.5f;
        var right = new Vector3(MathF.Cos(Yaw), 0f, -MathF.Sin(Yaw));
        var up = new Vector3(0f, halfHeight, 0f);
        var center = Position;

        var topLeft = center - right * halfWidth + up;
        var topRight = center + right * halfWidth + up;
        var bottomRight = center + right * halfWidth - up;
        var bottomLeft = center - right * halfWidth - up;
        return (topLeft, topRight, bottomRight, bottomLeft);
    }
}
