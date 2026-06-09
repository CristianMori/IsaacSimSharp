using System.Numerics;

namespace IsaacSimSharp.Robots;

/// <summary>
/// A snapshot of the sensed 6D reaction force/torque at each link's incoming joint
/// (force feedback). Unlike <see cref="DofState.Efforts"/> (commanded efforts), these
/// are the measured loads. <see cref="LinkNames"/>, <see cref="Forces"/>, and
/// <see cref="Torques"/> are parallel lists, one entry per link.
/// </summary>
public sealed record LinkForces(
    IReadOnlyList<string> LinkNames,
    IReadOnlyList<Vector3> Forces,  // newtons
    IReadOnlyList<Vector3> Torques); // newton-metres
