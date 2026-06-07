using System.Numerics;
using IsaacSimSharp.Protocol;
using IsaacSimSharp.Transport;

namespace IsaacSimSharp.Usd;

/// <summary>
/// Generic, reflective USD stage access: enumerate prims, define any prim type, and read/write
/// arbitrary attributes. Sits beneath the typed <see cref="Scene.SceneApi"/> helpers.
/// Obtained from <see cref="IsaacSimClient.Usd"/>.
/// </summary>
public sealed class UsdApi
{
    private readonly CommandChannel _commands;

    internal UsdApi(CommandChannel commands) => _commands = commands;

    /// <summary>Enumerates prims under <paramref name="root"/> (whole subtree when recursive).</summary>
    public async Task<IReadOnlyList<PrimInfo>> ListPrimsAsync(string root = "/", bool recursive = true, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { ListPrims = new ListPrimsRequest { Root = root, Recursive = recursive } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.PrimList.Prims;
    }

    /// <summary>Returns a prim's type, attribute names, and child paths (Exists=false if missing).</summary>
    public async Task<PrimDescReply> GetPrimAsync(string primPath, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetPrim = new GetPrimRequest { PrimPath = primPath } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.PrimDesc;
    }

    /// <summary>Defines a prim of any USD type (e.g. "Xform", "Sphere", "Mesh"); empty type = typeless.</summary>
    public async Task<string> DefinePrimAsync(string primPath, string typeName = "", CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { DefinePrim = new DefinePrimRequest { PrimPath = primPath, TypeName = typeName } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.Prim.PrimPath;
    }

    /// <summary>Reads an attribute value (Exists=false if the prim/attribute is absent or unset).</summary>
    public async Task<AttributeReply> GetAttributeAsync(string primPath, string name, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetAttribute = new GetAttributeRequest { PrimPath = primPath, Name = name } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.Attribute;
    }

    /// <summary>Writes an attribute value, creating the attribute (with an inferred type) if needed.</summary>
    public async Task SetAttributeAsync(string primPath, string name, UsdValue value, CancellationToken cancellationToken = default)
    {
        var request = new SetAttributeRequest { PrimPath = primPath, Name = name, Value = value };
        (await _commands.SendAsync(new Command { SetAttribute = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
    }

    /// <summary>Reads a prim's transform (world or local) as translation/orientation/scale.</summary>
    public async Task<Transform> GetTransformAsync(string primPath, bool world = true, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetTransform = new GetTransformRequest { PrimPath = primPath, World = world } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.Transform.Transform;
    }

    /// <summary>Writes a prim's transform; only the provided components are applied.</summary>
    public async Task SetTransformAsync(
        string primPath,
        Vector3? translation = null,
        Quaternion? orientation = null,
        Vector3? scale = null,
        bool world = true,
        CancellationToken cancellationToken = default)
    {
        var request = new SetTransformRequest { PrimPath = primPath, World = world };
        if (translation is { } t)
            request.Translation = new Vec3 { X = t.X, Y = t.Y, Z = t.Z };
        if (orientation is { } o)
            request.Orientation = new Quat { X = o.X, Y = o.Y, Z = o.Z, W = o.W };
        if (scale is { } s)
            request.Scale = new Vec3 { X = s.X, Y = s.Y, Z = s.Z };
        (await _commands.SendAsync(new Command { SetTransform = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
    }

    /// <summary>Returns the world-space axis-aligned bounding box of a prim.</summary>
    public async Task<BoundsReply> GetBoundsAsync(string primPath, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetBounds = new GetBoundsRequest { PrimPath = primPath } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.Bounds;
    }

    /// <summary>Finds prims under <paramref name="root"/> matching all the given filters (empty = ignored).</summary>
    public async Task<IReadOnlyList<PrimInfo>> FindPrimsAsync(
        string root = "/",
        string typeName = "",
        string nameRegex = "",
        string hasApi = "",
        CancellationToken cancellationToken = default)
    {
        var request = new FindPrimsRequest { Root = root, TypeName = typeName, NameRegex = nameRegex, HasApi = hasApi };
        var reply = (await _commands.SendAsync(new Command { FindPrims = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        return reply.PrimList.Prims;
    }

    /// <summary>Shows or hides a prim.</summary>
    public async Task SetVisibilityAsync(string primPath, bool visible, CancellationToken cancellationToken = default)
        => (await _commands.SendAsync(new Command { SetVisibility = new SetVisibilityRequest { PrimPath = primPath, Visible = visible } }, cancellationToken).ConfigureAwait(false)).EnsureOk();

    /// <summary>Activates or deactivates a prim (deactivated prims and their subtree are pruned).</summary>
    public async Task SetActiveAsync(string primPath, bool active, CancellationToken cancellationToken = default)
        => (await _commands.SendAsync(new Command { SetActive = new SetActiveRequest { PrimPath = primPath, Active = active } }, cancellationToken).ConfigureAwait(false)).EnsureOk();

    /// <summary>Applies an API schema (e.g. "PhysicsRigidBodyAPI", "PhysicsCollisionAPI", or any other).</summary>
    public async Task ApplySchemaAsync(string primPath, string schema, CancellationToken cancellationToken = default)
        => (await _commands.SendAsync(new Command { ApplySchema = new ApplySchemaRequest { PrimPath = primPath, Schema = schema } }, cancellationToken).ConfigureAwait(false)).EnsureOk();

    /// <summary>Sets the rigid-body mass (applies PhysicsMassAPI).</summary>
    public async Task SetMassAsync(string primPath, double mass, CancellationToken cancellationToken = default)
        => (await _commands.SendAsync(new Command { SetMass = new SetMassRequest { PrimPath = primPath, Mass = mass } }, cancellationToken).ConfigureAwait(false)).EnsureOk();

    /// <summary>Creates a UsdPreviewSurface material; returns its prim path.</summary>
    public async Task<string> CreateMaterialAsync(string primPath, Vector3 color, float metallic = 0f, float roughness = 0.5f, CancellationToken cancellationToken = default)
    {
        var request = new CreateMaterialRequest
        {
            PrimPath = primPath,
            Color = new Color { R = color.X, G = color.Y, B = color.Z },
            Metallic = metallic,
            Roughness = roughness,
        };
        var reply = (await _commands.SendAsync(new Command { CreateMaterial = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        return reply.Prim.PrimPath;
    }

    /// <summary>Binds a material to a prim.</summary>
    public async Task BindMaterialAsync(string primPath, string materialPath, CancellationToken cancellationToken = default)
        => (await _commands.SendAsync(new Command { BindMaterial = new BindMaterialRequest { PrimPath = primPath, MaterialPath = materialPath } }, cancellationToken).ConfigureAwait(false)).EnsureOk();

    // ---- typed convenience setters ----
    public Task SetAttributeAsync(string primPath, string name, bool value, CancellationToken cancellationToken = default)
        => SetAttributeAsync(primPath, name, new UsdValue { BoolValue = value }, cancellationToken);

    public Task SetAttributeAsync(string primPath, string name, long value, CancellationToken cancellationToken = default)
        => SetAttributeAsync(primPath, name, new UsdValue { IntValue = value }, cancellationToken);

    public Task SetAttributeAsync(string primPath, string name, double value, CancellationToken cancellationToken = default)
        => SetAttributeAsync(primPath, name, new UsdValue { DoubleValue = value }, cancellationToken);

    public Task SetAttributeAsync(string primPath, string name, string value, CancellationToken cancellationToken = default)
        => SetAttributeAsync(primPath, name, new UsdValue { StringValue = value }, cancellationToken);

    public Task SetAttributeAsync(string primPath, string name, Vector3 value, CancellationToken cancellationToken = default)
        => SetAttributeAsync(primPath, name, new UsdValue { Vec3Value = new Vec3 { X = value.X, Y = value.Y, Z = value.Z } }, cancellationToken);
}
