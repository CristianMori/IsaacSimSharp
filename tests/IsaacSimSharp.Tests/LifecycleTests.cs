using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Exercises the lifecycle API surface against the mock bridge (no GPU/Isaac Sim required).
/// End-to-end behavior against the real sim is verified by the Quickstart sample.
/// </summary>
[Collection(MockBridgeCollection.Name)]
public sealed class LifecycleTests
{
    private readonly MockBridgeFixture _fixture;

    public LifecycleTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Ack_operations_complete_without_error()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        await client.SetPhysicsDtAsync(1.0 / 60.0);
        await client.PlayAsync();
        await client.PauseAsync();
        await client.StopAsync();
        await client.ResetAsync();
    }

    [Fact]
    public async Task StepAsync_advances_frame_counter()
    {
        using var client = _fixture.CreateClient();
        var first = await client.StepAsync(10);
        var second = await client.StepAsync(5);
        Assert.True(second.Frame > first.Frame);
        Assert.Equal(first.Frame + 5u, second.Frame);
    }

    [Fact]
    public async Task ExportUsdAsync_returns_path()
    {
        using var client = _fixture.CreateClient();
        var path = await client.ExportUsdAsync(@"C:\tmp\scene.usda");
        Assert.Equal(@"C:\tmp\scene.usda", path);
    }
}
