// RuntimePhysics — set/read velocity and teleport a rigid body during simulation.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
var cube = await client.CreateCubeAsync("/World/Box", position: new Vector3(0, 0, 1), size: 0.5,
    collision: true, rigid: true);

await client.PlayAsync();
await client.StepAsync(5);

// give it an upward + spinning velocity
await cube.SetVelocityAsync(linear: new Vector3(0, 0, 5), angular: new Vector3(0, 0, 3));
await client.StepAsync(2);
var v = await cube.GetVelocityAsync();
Console.WriteLine($"velocity: lin.z={v.Linear.Z:F2} ang.z={v.Angular.Z:F2}");

// teleport it somewhere else mid-sim
await cube.SetWorldPoseAsync(new Vector3(2, 0, 4), Quaternion.Identity);
await client.StepAsync(2);
Console.WriteLine($"after teleport, z={(await cube.GetTransformAsync()).Translation.Z:F2}");
