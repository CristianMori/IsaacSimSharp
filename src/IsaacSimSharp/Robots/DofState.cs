namespace IsaacSimSharp.Robots;

/// <summary>A snapshot of an articulation's degrees of freedom.</summary>
public sealed record DofState(
    IReadOnlyList<double> Positions,
    IReadOnlyList<double> Velocities,
    IReadOnlyList<double> Efforts);
