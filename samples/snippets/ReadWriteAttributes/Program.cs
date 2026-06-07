// ReadWriteAttributes — generic USD attribute read/write (any prim, any attribute).
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Usd.DefinePrimAsync("/World/Thing", "Xform");

// typed convenience setters
await client.Usd.SetAttributeAsync("/World/Thing", "label", "hello");
await client.Usd.SetAttributeAsync("/World/Thing", "count", 42L);
await client.Usd.SetAttributeAsync("/World/Thing", "weight", 1.5);
await client.Usd.SetAttributeAsync("/World/Thing", "offset", new Vector3(1, 2, 3));

foreach (var name in new[] { "label", "count", "weight", "offset" })
{
    var a = await client.Usd.GetAttributeAsync("/World/Thing", name);
    Console.WriteLine($"{name}: type={a.TypeName} kind={a.Value.KindCase}");
}
