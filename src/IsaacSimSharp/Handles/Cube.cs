using IsaacSimSharp.Protocol;

namespace IsaacSimSharp.Handles;

/// <summary>A cube prim handle. Its size maps to the prim's scale.</summary>
public sealed class Cube : Prim
{
    internal Cube(IsaacSimClient client, string path) : base(client, path) { }

    /// <summary>Sets the cube's width/height/depth (its scale).</summary>
    public Task SetSizeAsync(double width, double height, double depth, CancellationToken cancellationToken = default)
        => SetScaleAsync(width, height, depth, cancellationToken);

    /// <summary>Loads the current transform and returns a <see cref="CubeEdit"/> (adds <c>Size</c>).</summary>
    public new async Task<CubeEdit> EditAsync(bool world = true, CancellationToken cancellationToken = default)
    {
        var t = await GetTransformAsync(world, cancellationToken).ConfigureAwait(false);
        return new CubeEdit(this, t, world);
    }
}

/// <summary>A <see cref="PrimEdit"/> with a <see cref="Size"/> view over the scale.</summary>
public sealed class CubeEdit : PrimEdit
{
    /// <summary>Width/Height/Depth, backed by the edit's scale.</summary>
    public Size3 Size { get; }

    internal CubeEdit(Cube cube, Transform current, bool world) : base(cube, current, world)
        => Size = new Size3(Scale);
}
