using System.Numerics;
using IsaacSimSharp.Protocol;

namespace IsaacSimSharp.Handles;

/// <summary>
/// A handle to a prim on the stage. Wraps a prim path + client and exposes object-style
/// async operations, plus <see cref="EditAsync"/> for fluent batched transform edits.
/// </summary>
public class Prim
{
    internal IsaacSimClient Client { get; }

    public string Path { get; }

    internal Prim(IsaacSimClient client, string path)
    {
        Client = client;
        Path = path;
    }

    /// <summary>Type, attributes, children, metadata, applied APIs, relationships.</summary>
    public Task<PrimDescReply> DescribeAsync(CancellationToken cancellationToken = default)
        => Client.Usd.GetPrimAsync(Path, cancellationToken);

    public Task<Transform> GetTransformAsync(bool world = true, CancellationToken cancellationToken = default)
        => Client.Usd.GetTransformAsync(Path, world, cancellationToken);

    public Task<BoundsReply> GetBoundsAsync(CancellationToken cancellationToken = default)
        => Client.Usd.GetBoundsAsync(Path, cancellationToken);

    public async Task<Vector3> GetPositionAsync(bool world = true, CancellationToken cancellationToken = default)
    {
        var t = await GetTransformAsync(world, cancellationToken).ConfigureAwait(false);
        return new Vector3((float)t.Translation.X, (float)t.Translation.Y, (float)t.Translation.Z);
    }

    public Task SetPositionAsync(double x, double y, double z, CancellationToken cancellationToken = default)
        => Client.Usd.SetTransformAsync(Path, translation: new Vector3((float)x, (float)y, (float)z), cancellationToken: cancellationToken);

    public Task SetPositionAsync(Vector3 position, CancellationToken cancellationToken = default)
        => Client.Usd.SetTransformAsync(Path, translation: position, cancellationToken: cancellationToken);

    public Task SetOrientationAsync(Quaternion orientation, CancellationToken cancellationToken = default)
        => Client.Usd.SetTransformAsync(Path, orientation: orientation, cancellationToken: cancellationToken);

    public Task SetScaleAsync(double x, double y, double z, CancellationToken cancellationToken = default)
        => Client.Usd.SetTransformAsync(Path, scale: new Vector3((float)x, (float)y, (float)z), cancellationToken: cancellationToken);

    public Task<AttributeReply> GetAttributeAsync(string name, CancellationToken cancellationToken = default)
        => Client.Usd.GetAttributeAsync(Path, name, cancellationToken);

    public Task SetAttributeAsync(string name, UsdValue value, CancellationToken cancellationToken = default)
        => Client.Usd.SetAttributeAsync(Path, name, value, cancellationToken);

    public Task RemoveAsync(CancellationToken cancellationToken = default)
        => Client.Scene.RemovePrimAsync(Path, cancellationToken);

    /// <summary>Moves this prim under a new parent (keeps its name); returns a handle to the new path.</summary>
    public async Task<Prim> ReparentAsync(string newParentPath, CancellationToken cancellationToken = default)
    {
        var name = Path[(Path.LastIndexOf('/') + 1)..];
        var dest = $"{newParentPath.TrimEnd('/')}/{name}";
        return new Prim(Client, await Client.Usd.MovePrimAsync(Path, dest, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Renames this prim within its parent; returns a handle to the new path.</summary>
    public async Task<Prim> RenameAsync(string newName, CancellationToken cancellationToken = default)
    {
        var parent = Path[..Path.LastIndexOf('/')];
        var dest = $"{(parent.Length == 0 ? "" : parent)}/{newName}";
        return new Prim(Client, await Client.Usd.MovePrimAsync(Path, dest, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Duplicates this prim to a new path; returns a handle to the copy.</summary>
    public async Task<Prim> DuplicateAsync(string newPath, CancellationToken cancellationToken = default)
        => new(Client, await Client.Usd.DuplicatePrimAsync(Path, newPath, cancellationToken).ConfigureAwait(false));

    public Task SetVisibleAsync(bool visible, CancellationToken cancellationToken = default)
        => Client.Usd.SetVisibilityAsync(Path, visible, cancellationToken);

    public Task SetActiveAsync(bool active, CancellationToken cancellationToken = default)
        => Client.Usd.SetActiveAsync(Path, active, cancellationToken);

    /// <summary>Makes this prim a dynamic rigid body (applies PhysicsRigidBodyAPI).</summary>
    public Task ApplyRigidBodyAsync(CancellationToken cancellationToken = default)
        => Client.Usd.ApplySchemaAsync(Path, "PhysicsRigidBodyAPI", cancellationToken);

    /// <summary>Gives this prim a collider (applies PhysicsCollisionAPI).</summary>
    public Task ApplyColliderAsync(CancellationToken cancellationToken = default)
        => Client.Usd.ApplySchemaAsync(Path, "PhysicsCollisionAPI", cancellationToken);

    public Task SetMassAsync(double mass, CancellationToken cancellationToken = default)
        => Client.Usd.SetMassAsync(Path, mass, cancellationToken);

    public Task BindMaterialAsync(string materialPath, CancellationToken cancellationToken = default)
        => Client.Usd.BindMaterialAsync(Path, materialPath, cancellationToken);

    // ---- runtime physics (when this prim is a rigid body) ----
    public Task SetVelocityAsync(Vector3 linear, Vector3 angular, CancellationToken cancellationToken = default)
        => Client.Physics.SetVelocityAsync(Path, linear, angular, cancellationToken);

    public Task<VelocityReply> GetVelocityAsync(CancellationToken cancellationToken = default)
        => Client.Physics.GetVelocityAsync(Path, cancellationToken);

    /// <summary>Teleports this rigid body to a world pose (immediate, even mid-sim).</summary>
    public Task SetWorldPoseAsync(Vector3 position, Quaternion orientation, CancellationToken cancellationToken = default)
        => Client.Physics.SetRigidPoseAsync(Path, position, orientation, cancellationToken);

    public async Task<IReadOnlyList<Prim>> GetChildrenAsync(CancellationToken cancellationToken = default)
    {
        var desc = await DescribeAsync(cancellationToken).ConfigureAwait(false);
        var children = new List<Prim>(desc.Children.Count);
        foreach (var path in desc.Children)
            children.Add(new Prim(Client, path));
        return children;
    }

    /// <summary>
    /// Loads the current transform and returns a <see cref="PrimEdit"/> you mutate locally
    /// (e.g. <c>e.Position.Y += 4</c>); the changes are written in one round-trip on
    /// <see cref="PrimEdit.ApplyAsync"/> / dispose.
    /// </summary>
    public async Task<PrimEdit> EditAsync(bool world = true, CancellationToken cancellationToken = default)
    {
        var t = await GetTransformAsync(world, cancellationToken).ConfigureAwait(false);
        return new PrimEdit(this, t, world);
    }
}

/// <summary>
/// A local, mutable snapshot of a prim's transform. Mutate <see cref="Position"/> /
/// <see cref="Orientation"/> / <see cref="Scale"/> freely (no I/O), then <see cref="ApplyAsync"/>
/// (or dispose) to write them back in a single command.
/// </summary>
public class PrimEdit : IAsyncDisposable
{
    private readonly Prim _prim;
    private readonly bool _world;
    private bool _applied;

    public Vec3Mut Position { get; }
    public QuatMut Orientation { get; }
    public Vec3Mut Scale { get; }

    internal PrimEdit(Prim prim, Transform current, bool world)
    {
        _prim = prim;
        _world = world;
        Position = new Vec3Mut(current.Translation.X, current.Translation.Y, current.Translation.Z);
        Orientation = new QuatMut
        {
            X = current.Orientation.X,
            Y = current.Orientation.Y,
            Z = current.Orientation.Z,
            W = current.Orientation.W,
        };
        Scale = new Vec3Mut(current.Scale.X, current.Scale.Y, current.Scale.Z);
    }

    /// <summary>Writes the edited transform back in one command (idempotent).</summary>
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (_applied)
            return;
        _applied = true;
        await _prim.Client.Usd.SetTransformAsync(
            _prim.Path,
            translation: new Vector3((float)Position.X, (float)Position.Y, (float)Position.Z),
            orientation: new Quaternion((float)Orientation.X, (float)Orientation.Y, (float)Orientation.Z, (float)Orientation.W),
            scale: new Vector3((float)Scale.X, (float)Scale.Y, (float)Scale.Z),
            world: _world,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() => await ApplyAsync().ConfigureAwait(false);
}
