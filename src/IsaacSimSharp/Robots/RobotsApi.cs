using IsaacSimSharp.Protocol;
using IsaacSimSharp.Transport;

namespace IsaacSimSharp.Robots;

/// <summary>
/// Robot operations: register an articulated prim so its joints can be read and driven.
/// Obtained from <see cref="IsaacSimClient.Robots"/>.
/// </summary>
public sealed class RobotsApi
{
    private readonly CommandChannel _commands;

    internal RobotsApi(CommandChannel commands) => _commands = commands;

    /// <summary>
    /// Wraps an articulated robot prim. The robot must already exist on the stage (e.g. via
    /// <see cref="Scene.SceneApi.AddReferenceAsync"/>); DOF metadata is most reliable once the
    /// simulation has been played and stepped at least once.
    /// </summary>
    public async Task<RobotArticulation> RegisterAsync(string primPath, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { RegisterArticulation = new RegisterArticulationRequest { PrimPath = primPath } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        var a = reply.Articulation;
        return new RobotArticulation(_commands, a.PrimPath, a.DofNames.ToArray(), a.DofCount);
    }
}
