// LidarSensor — attach an RTX lidar and read a point cloud.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Protocol;
using IsaacSimSharp.Scene;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddPrimitiveAsync("/World/Wall", PrimitiveShape.Cube, new Vector3(4, 0, 1), size: 2.0, collision: true);

var lidar = await client.Sensors.CreateLidarAsync("/World/lidar", position: new Vector3(0, 0, 1));
await client.PlayAsync();

// the rotary lidar fills its scan buffer over several frames
SensorFrame? frame = null;
for (var i = 0; i < 15 && (frame is null || frame.PointCloud.Count == 0); i++)
{
    await client.StepAsync(20);
    frame = await client.Sensors.GetFrameAsync(lidar);
}
Console.WriteLine($"lidar points: {frame!.PointCloud.Count} ({frame.PointCloud.Points.Length} bytes)");
