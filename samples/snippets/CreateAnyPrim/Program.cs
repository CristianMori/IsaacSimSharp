// CreateAnyPrim — instantiate prims of arbitrary USD types via DefinePrim.
// Prereq: a running bridge.
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();

foreach (var (path, type) in new[]
{
    ("/World/Group", "Xform"),
    ("/World/Group/Ball", "Sphere"),
    ("/World/Group/Panel", "Mesh"),
    ("/World/Group/Lamp", "SphereLight"),
    ("/World/Scope", "Scope"),
})
{
    var created = await client.Usd.DefinePrimAsync(path, type);
    Console.WriteLine($"defined {created} as {type}");
}
