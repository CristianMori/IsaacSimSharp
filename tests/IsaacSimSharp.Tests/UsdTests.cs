using System.Numerics;
using IsaacSimSharp.Protocol;
using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Exercises the generic USD API (enumerate / define / get / set) against the mock bridge's
/// in-memory stage. Real USD semantics are verified live against Isaac Sim.
/// </summary>
[Collection(MockBridgeCollection.Name)]
public sealed class UsdTests
{
    private readonly MockBridgeFixture _fixture;

    public UsdTests(MockBridgeFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DefineThenListAndGetPrim()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        await client.Usd.DefinePrimAsync("/World/Thing", "Xform");

        var prims = await client.Usd.ListPrimsAsync("/", recursive: true);
        Assert.Contains(prims, p => p.Path == "/World/Thing" && p.TypeName == "Xform");

        var desc = await client.Usd.GetPrimAsync("/World/Thing");
        Assert.True(desc.Exists);
        Assert.Equal("Xform", desc.TypeName);
    }

    [Fact]
    public async Task GetPrimReportsMissing()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        var desc = await client.Usd.GetPrimAsync("/World/Nope");
        Assert.False(desc.Exists);
    }

    [Fact]
    public async Task SetAttributeThenGetAttributeRoundtripsTypedValues()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        await client.Usd.DefinePrimAsync("/World/Thing", "Xform");

        await client.Usd.SetAttributeAsync("/World/Thing", "myDouble", 1.5);
        await client.Usd.SetAttributeAsync("/World/Thing", "myString", "hello");
        await client.Usd.SetAttributeAsync("/World/Thing", "myVec", new Vector3(1, 2, 3));

        var d = await client.Usd.GetAttributeAsync("/World/Thing", "myDouble");
        Assert.True(d.Exists);
        Assert.Equal(UsdValue.KindOneofCase.DoubleValue, d.Value.KindCase);
        Assert.Equal(1.5, d.Value.DoubleValue);

        var s = await client.Usd.GetAttributeAsync("/World/Thing", "myString");
        Assert.Equal("hello", s.Value.StringValue);

        var v = await client.Usd.GetAttributeAsync("/World/Thing", "myVec");
        Assert.Equal(1.0, v.Value.Vec3Value.X);
        Assert.Equal(3.0, v.Value.Vec3Value.Z);
    }

    [Fact]
    public async Task GetAttributeReportsMissing()
    {
        using var client = _fixture.CreateClient();
        await client.NewStageAsync();
        await client.Usd.DefinePrimAsync("/World/Thing", "Xform");
        var missing = await client.Usd.GetAttributeAsync("/World/Thing", "nope");
        Assert.False(missing.Exists);
    }
}
