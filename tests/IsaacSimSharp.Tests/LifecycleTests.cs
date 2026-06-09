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
    public async Task AckOperationsCompleteWithoutError()
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
    public async Task StepAsyncAdvancesFrameCounter()
    {
        using var client = _fixture.CreateClient();
        var first = await client.StepAsync(10);
        var second = await client.StepAsync(5);
        Assert.True(second.Frame > first.Frame);
        Assert.Equal(first.Frame + 5u, second.Frame);
    }

    [Fact]
    public async Task StepAsyncChunksLargeCounts()
    {
        using var client = _fixture.CreateClient();
        var before = await client.StepAsync(1);
        var after = await client.StepAsync(250); // > one chunk; client splits it transparently
        Assert.Equal(before.Frame + 250u, after.Frame);
    }

    [Fact]
    public async Task ExportUsdAsyncReturnsPath()
    {
        using var client = _fixture.CreateClient();
        var path = await client.ExportUsdAsync(@"C:\tmp\scene.usda");
        Assert.Equal(@"C:\tmp\scene.usda", path);
    }
}
