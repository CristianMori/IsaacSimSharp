using System.Numerics;
using IsaacSimSharp.Protocol;
using IsaacSimSharp.Transport;

namespace IsaacSimSharp.Robots;

/// <summary>
/// A handle to an articulated robot whose DOFs can be read and driven.
/// Obtained from <see cref="RobotsApi.RegisterAsync"/>.
/// </summary>
public sealed class RobotArticulation
{
    private readonly CommandChannel _commands;

    internal RobotArticulation(CommandChannel commands, string primPath, IReadOnlyList<string> dofNames, int dofCount)
    {
        _commands = commands;
        PrimPath = primPath;
        DofNames = dofNames;
        DofCount = dofCount;
    }

    public string PrimPath { get; }

    /// <summary>Joint names in DOF order (may be empty if unavailable before the sim is playing).</summary>
    public IReadOnlyList<string> DofNames { get; }

    /// <summary>Number of degrees of freedom.</summary>
    public int DofCount { get; }

    /// <summary>Reads current joint positions, velocities, and efforts.</summary>
    public async Task<DofState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetDofState = new GetDofStateRequest { PrimPath = PrimPath } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        var s = reply.DofState;
        return new DofState(s.Positions.ToArray(), s.Velocities.ToArray(), s.Efforts.ToArray());
    }

    /// <summary>
    /// Reads the sensed 6D reaction force/torque at each link's incoming joint (force feedback).
    /// Distinct from <see cref="DofState.Efforts"/>, which are commanded efforts.
    /// </summary>
    public async Task<LinkForces> GetLinkForcesAsync(CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetLinkForces = new GetLinkForcesRequest { PrimPath = PrimPath } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        var f = reply.LinkForces;
        return new LinkForces(f.LinkNames.ToArray(), ToVectors(f.Forces), ToVectors(f.Torques));
    }

    /// <summary>Sets joint position targets (PD position control).</summary>
    public Task SetPositionTargetsAsync(IReadOnlyList<double> targets, IReadOnlyList<int>? dofIndices = null, CancellationToken cancellationToken = default)
        => SetAsync(DofControlMode.DofPosition, targets, dofIndices, cancellationToken);

    /// <summary>Sets joint velocity targets (velocity control).</summary>
    public Task SetVelocityTargetsAsync(IReadOnlyList<double> targets, IReadOnlyList<int>? dofIndices = null, CancellationToken cancellationToken = default)
        => SetAsync(DofControlMode.DofVelocity, targets, dofIndices, cancellationToken);

    /// <summary>Applies direct joint efforts/torques.</summary>
    public Task SetEffortsAsync(IReadOnlyList<double> efforts, IReadOnlyList<int>? dofIndices = null, CancellationToken cancellationToken = default)
        => SetAsync(DofControlMode.DofEffort, efforts, dofIndices, cancellationToken);

    private async Task SetAsync(DofControlMode mode, IReadOnlyList<double> values, IReadOnlyList<int>? indices, CancellationToken cancellationToken)
    {
        var request = new SetDofTargetsRequest { PrimPath = PrimPath, Mode = mode };
        request.Values.AddRange(values);
        if (indices is not null)
            request.Indices.AddRange(indices.Select(i => (uint)i));

        (await _commands.SendAsync(new Command { SetDofTargets = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
    }

    /// <summary>Unpacks a flattened (link_count * 3) list into one <see cref="Vector3"/> per link.</summary>
    private static IReadOnlyList<Vector3> ToVectors(IReadOnlyList<double> flat)
    {
        var result = new Vector3[flat.Count / 3];
        for (var i = 0; i < result.Length; i++)
            result[i] = new Vector3((float)flat[i * 3], (float)flat[i * 3 + 1], (float)flat[i * 3 + 2]);
        return result;
    }
}
