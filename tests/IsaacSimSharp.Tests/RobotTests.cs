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
    public async Task GetAssetsRootReturnsPath()
    {
        using var client = _fixture.CreateClient();
        Assert.False(string.IsNullOrEmpty(await client.GetAssetsRootAsync()));
    }

    [Fact]
    public async Task RegisterReturnsDofMetadata()
    {
        using var client = _fixture.CreateClient();
        var robot = await client.Robots.RegisterAsync("/World/robot");
        Assert.Equal("/World/robot", robot.PrimPath);
        Assert.Equal(9, robot.DofCount);
        Assert.Equal(9, robot.DofNames.Count);
    }

    [Fact]
    public async Task SetPositionTargetsThenGetStateReflectsTargets()
    {
        using var client = _fixture.CreateClient();
        var robot = await client.Robots.RegisterAsync("/World/robot");
        double[] target = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.0, 0.0 };
        await robot.SetPositionTargetsAsync(target);

        var state = await robot.GetStateAsync();
        Assert.Equal(target, state.Positions);
    }
}
