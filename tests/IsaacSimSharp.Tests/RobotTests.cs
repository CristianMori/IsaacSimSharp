using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Exercises the Robots API surface against the mock bridge (no GPU required).
/// Real articulation behavior is verified by the RobotControl sample against Isaac Sim.
/// </summary>
[Collection(MockBridgeCollection.Name)]
public sealed class RobotTests
{
    private readonly MockBridgeFixture _fixture;

    public RobotTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetAssetsRoot_returns_path()
    {
        using var client = IsaacSimClient.Connect(_fixture.Endpoint);
        Assert.False(string.IsNullOrEmpty(await client.GetAssetsRootAsync()));
    }

    [Fact]
    public async Task Register_returns_dof_metadata()
    {
        using var client = IsaacSimClient.Connect(_fixture.Endpoint);
        var robot = await client.Robots.RegisterAsync("/World/robot");
        Assert.Equal("/World/robot", robot.PrimPath);
        Assert.Equal(9, robot.DofCount);
        Assert.Equal(9, robot.DofNames.Count);
    }

    [Fact]
    public async Task SetPositionTargets_then_GetState_reflects_targets()
    {
        using var client = IsaacSimClient.Connect(_fixture.Endpoint);
        var robot = await client.Robots.RegisterAsync("/World/robot");
        double[] target = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.0, 0.0 };
        await robot.SetPositionTargetsAsync(target);

        var state = await robot.GetStateAsync();
        Assert.Equal(target, state.Positions);
    }
}
