// TransformsAndBounds — read/write a prim's transform and query its world AABB.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
var cube = await client.CreateCubeAsync("/World/Box", size: 1.0);

await cube.SetPositionAsync(2, 1, 3);
await cube.SetScaleAsync(0.5, 1.0, 2.0);

var t = await cube.GetTransformAsync();
Console.WriteLine($"pos=({t.Translation.X},{t.Translation.Y},{t.Translation.Z}) " +
                  $"scale=({t.Scale.X},{t.Scale.Y},{t.Scale.Z})");

var b = await cube.GetBoundsAsync();
Console.WriteLine($"world AABB: min=({b.Min.X:F2},{b.Min.Y:F2},{b.Min.Z:F2}) max=({b.Max.X:F2},{b.Max.Y:F2},{b.Max.Z:F2})");
