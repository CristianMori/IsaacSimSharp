// ExportUsd — build a scene and export it to a USD file (the digital-twin handoff artifact).
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Scene;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube, new Vector3(0, 0, 0.5f), size: 0.5);

var path = Path.GetFullPath("exported_scene.usda");
var written = await client.ExportUsdAsync(path);
Console.WriteLine($"Exported stage -> {written}");
