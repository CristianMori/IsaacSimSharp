// PhysicsAuthoring — turn a plain prim into a dynamic rigid body with a collider and mass.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();

var cube = await client.CreateCubeAsync("/World/Box", position: new Vector3(0, 0, 3), size: 0.5);
await cube.ApplyColliderAsync();
await cube.ApplyRigidBodyAsync();
await cube.SetMassAsync(2.0);

await client.PlayAsync();
await client.StepAsync(120); // it now falls under gravity and lands on the ground

var t = await cube.GetTransformAsync();
Console.WriteLine($"box settled at z={t.Translation.Z:F2} (started at 3.0)");
