// ImuSensor — attach an IMU to a body and read it.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
// Start the cube resting on the ground (half its size above z=0) so the IMU reads the
// ~9.81 m/s^2 gravity reaction. (In free fall the proper acceleration would read ~0.)
var cube = await client.CreateCubeAsync("/World/Box", position: new Vector3(0, 0, 0.25f), size: 0.5,
    collision: true, rigid: true);

var imu = await client.Sensors.CreateImuAsync("/World/Box/imu");
await client.PlayAsync();
await client.StepAsync(60);

var frame = await client.Sensors.GetFrameAsync(imu);
var a = frame.Imu.LinearAcceleration;
var o = frame.Imu.Orientation;
Console.WriteLine($"IMU accel = ({a.X:F2}, {a.Y:F2}, {a.Z:F2}) m/s^2  (≈ +9.81 on z when at rest)");
Console.WriteLine($"IMU orientation (wxyz) = ({o.W:F2}, {o.X:F2}, {o.Y:F2}, {o.Z:F2})");
