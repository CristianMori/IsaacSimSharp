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
    public async Task ApplyRigidBody_shows_in_prim_apis()
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
    public async Task Material_create_and_bind()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var cube = await client.CreateCubeAsync("/World/Box");
        var mat = await client.Usd.CreateMaterialAsync("/World/Materials/Red", new Vector3(1, 0, 0), roughness: 0.3f);
        Assert.Equal("/World/Materials/Red", mat);
        await cube.BindMaterialAsync(mat); // no throw
    }

    [Fact]
    public async Task SetActive_then_describe_reflects_state()
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
    public async Task Velocity_roundtrips()
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
    public async Task Raycast_returns_hit()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var hit = await client.Physics.RaycastAsync(new Vector3(0, 0, 10), new Vector3(0, 0, -1), 100);
        Assert.True(hit.Hit);
        Assert.True(hit.Distance > 0);
    }
}
