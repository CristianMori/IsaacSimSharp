// VisibilityAndActive — hide/show and activate/deactivate prims.
// Prereq: a running bridge.
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
var cube = await client.CreateCubeAsync("/World/Box");

await cube.SetVisibleAsync(false);
Console.WriteLine("hidden");
await cube.SetVisibleAsync(true);
Console.WriteLine("shown again");

await cube.SetActiveAsync(false); // prunes the prim (and subtree) from composition
Console.WriteLine($"active after deactivate: {(await cube.DescribeAsync()).Active}");
await cube.SetActiveAsync(true);
Console.WriteLine($"active after reactivate: {(await cube.DescribeAsync()).Active}");
