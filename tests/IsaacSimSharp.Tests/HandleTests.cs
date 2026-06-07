using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Exercises the handle/Edit paradigm and the transform/find verbs against the mock's
/// in-memory stage.
/// </summary>
[Collection(MockBridgeCollection.Name)]
public sealed class HandleTests
{
    private readonly MockBridgeFixture _fixture;

    public HandleTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Cube_handle_set_and_get_position()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box");

        await cube.SetPositionAsync(1, 2, 3);
        var pos = await cube.GetPositionAsync();
        Assert.Equal(1, pos.X, 3);
        Assert.Equal(2, pos.Y, 3);
        Assert.Equal(3, pos.Z, 3);
    }

    [Fact]
    public async Task Edit_scope_batches_fluent_mutations()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box", size: 1.0);
        await cube.SetPositionAsync(0, 0, 0);

        await using (var e = await cube.EditAsync())
        {
            e.Position.Y += 4;   // the paradigm from the request
            e.Size.Width = 30;
        } // flushed on dispose

        var t = await cube.GetTransformAsync();
        Assert.Equal(4, t.Translation.Y, 3);
        Assert.Equal(30, t.Scale.X, 3);
    }

    [Fact]
    public async Task FindPrims_filters_by_type()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        await client.CreateCubeAsync("/World/A");
        await client.CreateCubeAsync("/World/B");
        await client.DefinePrimAsync("/World/Light1", "DistantLight");

        var cubes = await client.Usd.FindPrimsAsync(typeName: "Cube");
        Assert.Equal(2, cubes.Count);
        Assert.All(cubes, p => Assert.Equal("Cube", p.TypeName));
    }

    [Fact]
    public async Task Rename_reparent_duplicate()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        await client.DefinePrimAsync("/World/Group", "Xform");
        var cube = await client.CreateCubeAsync("/World/Box");

        var renamed = await cube.RenameAsync("Box2");
        Assert.Equal("/World/Box2", renamed.Path);

        var moved = await renamed.ReparentAsync("/World/Group");
        Assert.Equal("/World/Group/Box2", moved.Path);

        var copy = await moved.DuplicateAsync("/World/Group/Box3");
        Assert.Equal("/World/Group/Box3", copy.Path);

        var cubes = await client.Usd.FindPrimsAsync(typeName: "Cube");
        Assert.Equal(2, cubes.Count); // moved original + duplicate
    }

    [Fact]
    public async Task GetBounds_reflects_position_and_size()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box", size: 2.0);
        await cube.SetPositionAsync(10, 0, 0);

        var b = await cube.GetBoundsAsync();
        Assert.True(b.Valid);
        Assert.Equal(9, b.Min.X, 3);
        Assert.Equal(11, b.Max.X, 3);
    }
}
