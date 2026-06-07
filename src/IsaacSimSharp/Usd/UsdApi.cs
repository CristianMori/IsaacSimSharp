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
