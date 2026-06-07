// ImuSensor — attach an IMU to a body and read it.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
var cube = await client.CreateCubeAsync("/World/Box", position: new Vector3(0, 0, 1), size: 0.5,
    collision: true, rigid: true);

var imu = await client.Sensors.CreateImuAsync("/World/Box/imu");
await client.PlayAsync();
await client.StepAsync(10);

var frame = await client.Sensors.GetFrameAsync(imu);
var a = frame.Imu.LinearAcceleration;
Console.WriteLine($"IMU accel = ({a.X:F2}, {a.Y:F2}, {a.Z:F2}) m/s^2");
