namespace IsaacSimSharp.Handles;

/// <summary>A mutable 3-vector used inside <see cref="PrimEdit"/> for fluent edits (e.g. <c>Position.Y += 4</c>).</summary>
public sealed class Vec3Mut
{
    public double X;
    public double Y;
    public double Z;

    public Vec3Mut() { }
    public Vec3Mut(double x, double y, double z) { X = x; Y = y; Z = z; }
}

/// <summary>A mutable quaternion (wxyz) used inside <see cref="PrimEdit"/>.</summary>
public sealed class QuatMut
{
    public double X;
    public double Y;
    public double Z;
    public double W = 1.0;
}

/// <summary>Width/Height/Depth view over a cube's scale, for <c>Size.Width = 30</c>-style edits.</summary>
public sealed class Size3
{
    private readonly Vec3Mut _scale;

    internal Size3(Vec3Mut scale) => _scale = scale;

    public double Width { get => _scale.X; set => _scale.X = value; }
    public double Height { get => _scale.Y; set => _scale.Y = value; }
    public double Depth { get => _scale.Z; set => _scale.Z = value; }
}
