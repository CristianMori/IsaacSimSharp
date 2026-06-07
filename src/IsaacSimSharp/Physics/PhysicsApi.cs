using System.Numerics;
using IsaacSimSharp.Protocol;
using IsaacSimSharp.Transport;

namespace IsaacSimSharp.Physics;

/// <summary>
/// Runtime physics: teleport/impart velocity on rigid bodies during simulation, and scene queries.
/// Obtained from <see cref="IsaacSimClient.Physics"/>.
/// </summary>
public sealed class PhysicsApi
{
    private readonly CommandChannel _commands;

    internal PhysicsApi(CommandChannel commands) => _commands = commands;

    /// <summary>Teleports a rigid body to a world pose (takes effect immediately, even mid-sim).</summary>
    public async Task SetRigidPoseAsync(string primPath, Vector3 position, Quaternion orientation, CancellationToken cancellationToken = default)
    {
        var request = new SetRigidPoseRequest
        {
            PrimPath = primPath,
            Position = new Vec3 { X = position.X, Y = position.Y, Z = position.Z },
            Orientation = new Quat { X = orientation.X, Y = orientation.Y, Z = orientation.Z, W = orientation.W },
        };
        (await _commands.SendAsync(new Command { SetRigidPose = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
    }

    /// <summary>Sets a rigid body's linear and angular velocity.</summary>
    public async Task SetVelocityAsync(string primPath, Vector3 linear, Vector3 angular, CancellationToken cancellationToken = default)
    {
        var request = new SetVelocityRequest
        {
            PrimPath = primPath,
            Linear = new Vec3 { X = linear.X, Y = linear.Y, Z = linear.Z },
            Angular = new Vec3 { X = angular.X, Y = angular.Y, Z = angular.Z },
        };
        (await _commands.SendAsync(new Command { SetVelocity = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
    }

    /// <summary>Reads a rigid body's linear and angular velocity.</summary>
    public async Task<VelocityReply> GetVelocityAsync(string primPath, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetVelocity = new GetVelocityRequest { PrimPath = primPath } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.Velocity;
    }

    /// <summary>Closest-hit raycast against the physics scene.</summary>
    public async Task<RaycastReply> RaycastAsync(Vector3 origin, Vector3 direction, double maxDistance, CancellationToken cancellationToken = default)
    {
        var request = new RaycastRequest
        {
            Origin = new Vec3 { X = origin.X, Y = origin.Y, Z = origin.Z },
            Direction = new Vec3 { X = direction.X, Y = direction.Y, Z = direction.Z },
            MaxDistance = maxDistance,
        };
        var reply = (await _commands.SendAsync(new Command { Raycast = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        return reply.Raycast;
    }
}
