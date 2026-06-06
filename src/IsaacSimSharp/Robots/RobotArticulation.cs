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
}
