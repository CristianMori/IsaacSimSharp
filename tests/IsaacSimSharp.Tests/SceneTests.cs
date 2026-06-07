using System.Numerics;
using IsaacSimSharp.Scene;
using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Exercises the Scene API surface against the mock bridge (no GPU required).
/// Real geometry/physics behavior is verified by the Quickstart sample against Isaac Sim.
/// </summary>
[Collection(MockBridgeCollection.Name)]
public sealed class SceneTests
{
    private readonly MockBridgeFixture _fixture;

    public SceneTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AddGroundPlane_returns_prim_path()
    {
        using var client = _fixture.CreateClient();
        Assert.Equal("/World/GroundPlane", await client.Scene.AddGroundPlaneAsync());
    }

    [Fact]
    public async Task AddLight_returns_requested_path()
    {
        using var client = _fixture.CreateClient();
        Assert.Equal("/World/Sun", await client.Scene.AddLightAsync("/World/Sun", LightKind.Distant, 1500));
    }

    [Fact]
    public async Task AddPrimitive_returns_requested_path()
    {
        using var client = _fixture.CreateClient();
        var path = await client.Scene.AddPrimitiveAsync(
            "/World/Box", PrimitiveShape.Cube, new Vector3(0, 0, 1), size: 0.3, collision: true, rigid: true);
        Assert.Equal("/World/Box", path);
    }

    [Fact]
    public async Task AddReference_and_ImportUrdf_return_paths()
    {
        using var client = _fixture.CreateClient();
        Assert.Equal("/World/Robot", await client.Scene.AddReferenceAsync("omniverse://robot.usd", "/World/Robot"));
        Assert.Equal("/World/Arm", await client.Scene.ImportUrdfAsync(@"C:\arm.urdf", "/World/Arm"));
    }

    [Fact]
    public async Task SetPrimPose_and_RemovePrim_complete()
    {
        using var client = _fixture.CreateClient();
        await client.Scene.SetPrimPoseAsync("/World/Box", new Vector3(2, 1, 3));
        await client.Scene.RemovePrimAsync("/World/Box");
    }
}
