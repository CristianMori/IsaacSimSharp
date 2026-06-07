// EnumerateStage — list prims, find by type, and inspect a prim's details.
// Prereq: a running bridge.
using IsaacSimSharp;
using IsaacSimSharp.Scene;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun");
await client.CreateCubeAsync("/World/Box");

Console.WriteLine("All prims under /World:");
foreach (var p in await client.Usd.ListPrimsAsync("/World", recursive: true))
    Console.WriteLine($"  {p.Path}  ({p.TypeName})");

Console.WriteLine("\nCubes only (FindPrims):");
foreach (var p in await client.Usd.FindPrimsAsync(typeName: "Cube"))
    Console.WriteLine($"  {p.Path}");

var desc = await client.Usd.GetPrimAsync("/World/Box");
Console.WriteLine($"\n/World/Box: type={desc.TypeName} active={desc.Active} visibility={desc.Visibility} " +
                  $"attrs={desc.Attributes.Count} apis=[{string.Join(", ", desc.AppliedApis)}]");
