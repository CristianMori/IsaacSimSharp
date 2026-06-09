// DepthPointCloud — capture a depth frame and deproject it into a 3D point cloud.
// The camera frame carries pinhole intrinsics (fx, fy, cx, cy); DepthCloud.ToPoints
// uses them to turn the depth image into camera-space points.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Scene;
using IsaacSimSharp.Sensors;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube, new Vector3(0, 0, 0.5f), size: 0.5);

var q = 1.0f / MathF.Sqrt(2);
var cam = await client.Sensors.CreateCameraAsync("/World/cam", 640, 480,
    position: new Vector3(0, -3f, 0.8f), orientation: new Quaternion(q, 0, 0, q), depth: true); // look +Y

await client.PlayAsync();
await client.StepAsync(30); // let the renderer warm up

var frame = await client.Sensors.GetFrameAsync(cam);
var k = frame.Image.Intrinsics;
Console.WriteLine($"intrinsics: fx={k.Fx:F1} fy={k.Fy:F1} cx={k.Cx:F1} cy={k.Cy:F1}");

var points = DepthCloud.ToPoints(frame.Image);
Console.WriteLine($"deprojected {points.Count} points from a {frame.Image.Width}x{frame.Image.Height} depth image");

if (points.Count > 0)
{
    var zs = points.Select(p => p.Z).ToArray();
    Console.WriteLine($"depth range: {zs.Min():F3} .. {zs.Max():F3} m");
    Console.WriteLine($"first point (camera frame): {points[0]}");
}
