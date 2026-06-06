using Xunit;

namespace IsaacSimSharp.Tests;

[Collection(MockBridgeCollection.Name)]
public sealed class HandshakeTests
{
    private readonly MockBridgeFixture _fixture;

    public HandshakeTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PingAsync_echoes_message()
    {
        using var client = IsaacSimClient.Connect(_fixture.Endpoint);
        var result = await client.PingAsync("hello isaac");
        Assert.Equal("hello isaac", result);
    }

    [Fact]
    public async Task GetVersionAsync_returns_protocol_version()
    {
        using var client = IsaacSimClient.Connect(_fixture.Endpoint);
        var version = await client.GetVersionAsync();
        Assert.Equal("0.1.0", version.ProtocolVersion);
        Assert.False(string.IsNullOrEmpty(version.BridgeVersion));
    }

    [Fact]
    public async Task PingAsync_times_out_when_no_bridge()
    {
        using var client = IsaacSimClient.Connect("tcp://127.0.0.1:5999"); // nothing listening
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
        await Assert.ThrowsAnyAsync<Exception>(async () => await client.PingAsync("x", cts.Token));
    }
}
