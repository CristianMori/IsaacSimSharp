namespace IsaacSimSharp.Scene;

/// <summary>Primitive geometry kinds. Values mirror <see cref="Protocol.Shape"/> order.</summary>
public enum PrimitiveShape
{
    Cube = 0,
    Sphere = 1,
    Cylinder = 2,
    Cone = 3,
    Capsule = 4,
}

/// <summary>Light kinds. Values mirror <see cref="Protocol.LightType"/> order.</summary>
public enum LightKind
{
    Distant = 0,
    Sphere = 1,
    Dome = 2,
    Rect = 3,
}
