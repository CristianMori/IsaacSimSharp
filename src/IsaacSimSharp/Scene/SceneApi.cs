using System.Numerics;
using IsaacSimSharp.Protocol;
using IsaacSimSharp.Transport;

namespace IsaacSimSharp.Scene;

/// <summary>
/// Scene-configuration operations: ground plane, lights, primitives, references, poses.
/// Obtained from <see cref="IsaacSimClient.Scene"/>.
/// </summary>
public sealed class SceneApi
{
    private readonly CommandChannel _commands;

    internal SceneApi(CommandChannel commands) => _commands = commands;

    /// <summary>Adds a default ground plane; returns the created prim path.</summary>
    public Task<string> AddGroundPlaneAsync(string primPath = "/World/GroundPlane", CancellationToken cancellationToken = default)
        => PrimAsync(new Command { AddGroundPlane = new AddGroundPlaneRequest { PrimPath = primPath } }, cancellationToken);

    /// <summary>Adds a light of the given kind and intensity; returns the created prim path.</summary>
    public Task<string> AddLightAsync(
        string primPath = "/World/Light",
        LightKind kind = LightKind.Distant,
        double intensity = 1000.0,
        CancellationToken cancellationToken = default)
        => PrimAsync(
            new Command
            {
                AddLight = new AddLightRequest
                {
                    PrimPath = primPath,
                    Type = (LightType)(int)kind,
                    Intensity = intensity,
                },
            },
            cancellationToken);

    /// <summary>Adds a primitive shape; returns the created prim path.</summary>
    public Task<string> AddPrimitiveAsync(
        string primPath,
        PrimitiveShape shape,
        Vector3 position = default,
        double size = 0.5,
        Quaternion? orientation = null,
        Vector3? scale = null,
        bool collision = false,
        bool rigid = false,
        CancellationToken cancellationToken = default)
    {
        var request = new AddPrimitiveRequest
        {
            PrimPath = primPath,
            Shape = (Protocol.Shape)(int)shape,
            Position = ToVec3(position),
            Size = size,
            Collision = collision,
            Rigid = rigid,
        };
        if (orientation is { } q)
            request.Orientation = ToQuat(q);
        if (scale is { } s)
            request.Scale = ToVec3(s);
        return PrimAsync(new Command { AddPrimitive = request }, cancellationToken);
    }

    /// <summary>References an external USD asset (e.g. a robot) onto the stage; returns the prim path.</summary>
    public Task<string> AddReferenceAsync(string usdPath, string primPath, CancellationToken cancellationToken = default)
        => PrimAsync(
            new Command { AddReference = new AddReferenceRequest { UsdPath = usdPath, PrimPath = primPath } },
            cancellationToken);

    /// <summary>Imports a URDF robot to USD; returns the created prim path.</summary>
    public Task<string> ImportUrdfAsync(string urdfPath, string primPath = "", bool fixedBase = true, CancellationToken cancellationToken = default)
        => PrimAsync(
            new Command { ImportUrdf = new ImportUrdfRequest { UrdfPath = urdfPath, PrimPath = primPath, FixedBase = fixedBase } },
            cancellationToken);

    /// <summary>Sets the world pose of an existing prim.</summary>
    public async Task SetPrimPoseAsync(string primPath, Vector3 position, Quaternion? orientation = null, CancellationToken cancellationToken = default)
    {
        var request = new SetPrimPoseRequest { PrimPath = primPath, Position = ToVec3(position) };
        if (orientation is { } q)
            request.Orientation = ToQuat(q);
        var reply = await _commands.SendAsync(new Command { SetPrimPose = request }, cancellationToken).ConfigureAwait(false);
        reply.EnsureOk();
    }

    /// <summary>Removes a prim and its descendants from the stage.</summary>
    public async Task RemovePrimAsync(string primPath, CancellationToken cancellationToken = default)
    {
        var reply = await _commands
            .SendAsync(new Command { RemovePrim = new RemovePrimRequest { PrimPath = primPath } }, cancellationToken)
            .ConfigureAwait(false);
        reply.EnsureOk();
    }

    private async Task<string> PrimAsync(Command command, CancellationToken cancellationToken)
    {
        var reply = await _commands.SendAsync(command, cancellationToken).ConfigureAwait(false);
        reply.EnsureOk();
        return reply.Prim.PrimPath;
    }

    private static Vec3 ToVec3(Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };

    private static Quat ToQuat(Quaternion q) => new() { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
}
