// ReparentRenameDuplicate — structural edits on the stage via handle methods.
// Prereq: a running bridge.
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.DefinePrimAsync("/World/Group", "Xform");
var cube = await client.CreateCubeAsync("/World/Box");

var renamed = await cube.RenameAsync("Crate");        // /World/Box -> /World/Crate
Console.WriteLine($"renamed   -> {renamed.Path}");

var moved = await renamed.ReparentAsync("/World/Group"); // -> /World/Group/Crate
Console.WriteLine($"reparented-> {moved.Path}");

var copy = await moved.DuplicateAsync("/World/Group/Crate2");
Console.WriteLine($"duplicated-> {copy.Path}");

foreach (var p in await client.Usd.FindPrimsAsync(typeName: "Cube"))
    Console.WriteLine($"  cube: {p.Path}");
