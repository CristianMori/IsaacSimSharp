// Materials — create a UsdPreviewSurface material and bind it to a prim.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
var cube = await client.CreateCubeAsync("/World/Box", size: 0.5);

// color is RGB in 0..1
var red = await client.Usd.CreateMaterialAsync("/World/Materials/Red", new Vector3(0.9f, 0.1f, 0.1f),
    metallic: 0.0f, roughness: 0.3f);
await cube.BindMaterialAsync(red);
Console.WriteLine($"created {red} and bound it to {cube.Path}");
