// BuildScene — create a ground plane, a light, and a couple of primitives, then simulate.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Scene;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);

await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", LightKind.Distant, intensity: 1500);
await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube,
    position: new Vector3(0, 0, 1.0f), size: 0.3, collision: true, rigid: true);
await client.Scene.AddPrimitiveAsync("/World/Ball", PrimitiveShape.Sphere,
    position: new Vector3(0.4f, 0, 2.0f), size: 0.25, collision: true, rigid: true);

await client.PlayAsync();
var step = await client.StepAsync(90); // the rigid bodies fall and settle
Console.WriteLine($"Built scene; simulated to {step.SimTime:F2}s.");
