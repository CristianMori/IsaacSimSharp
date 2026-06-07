using System.Numerics;
using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Phase B manipulation/physics surface against the mock (apply-schema, materials, visibility,
/// runtime velocity/pose, raycast). Real physics/USD semantics are verified live.
/// </summary>
[Collection(MockBridgeCollection.Name)]
public sealed class ManipulationTests
{
    private readonly MockBridgeFixture _fixture;

    public ManipulationTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ApplyRigidBodyShowsInPrimApis()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box");
        await cube.ApplyRigidBodyAsync();
        await cube.SetMassAsync(2.5);

        var desc = await cube.DescribeAsync();
        Assert.Contains("PhysicsRigidBodyAPI", desc.AppliedApis);
    }

    [Fact]
    public async Task MaterialCreateAndBind()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box");
        var mat = await client.Usd.CreateMaterialAsync("/World/Materials/Red", new Vector3(1, 0, 0), roughness: 0.3f);
        Assert.Equal("/World/Materials/Red", mat);
        await cube.BindMaterialAsync(mat); // no throw
    }

    [Fact]
    public async Task SetActiveThenDescribeReflectsState()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box");
        await cube.SetActiveAsync(false);
        await cube.SetVisibleAsync(false); // no throw
        var desc = await cube.DescribeAsync();
        Assert.False(desc.Active);
    }

    [Fact]
    public async Task VelocityRoundtrips()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box");
        await cube.ApplyRigidBodyAsync();
        await cube.SetVelocityAsync(new Vector3(1, 0, 0), new Vector3(0, 0, 2));

        var v = await cube.GetVelocityAsync();
        Assert.Equal(1, v.Linear.X, 3);
        Assert.Equal(2, v.Angular.Z, 3);
    }

    [Fact]
    public async Task RaycastReturnsHit()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var hit = await client.Physics.RaycastAsync(new Vector3(0, 0, 10), new Vector3(0, 0, -1), 100);
        Assert.True(hit.Hit);
        Assert.True(hit.Distance > 0);
    }
}
