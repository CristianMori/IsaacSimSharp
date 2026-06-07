// Raycast — query the physics scene with a closest-hit raycast.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Scene;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube,
    new Vector3(0, 0, 1.0f), size: 0.5, collision: true);
await client.PlayAsync();
await client.StepAsync(5);

// Cast straight down from above the box.
var hit = await client.Physics.RaycastAsync(new Vector3(0, 0, 10), new Vector3(0, 0, -1), maxDistance: 100);
Console.WriteLine(hit.Hit
    ? $"hit {hit.PrimPath} at ({hit.Position.X:F2},{hit.Position.Y:F2},{hit.Position.Z:F2}) dist={hit.Distance:F2}"
    : "no hit");
