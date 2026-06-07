// CameraSnapshot — attach an RTX camera, pull one frame, and save it as a PNG.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Imaging;
using IsaacSimSharp.Scene;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube, new Vector3(0, 0, 0.5f), size: 0.5);

var q = 1.0f / MathF.Sqrt(2);
var cam = await client.Sensors.CreateCameraAsync("/World/cam", 640, 480,
    position: new Vector3(0, -3f, 0.8f), orientation: new Quaternion(q, 0, 0, q)); // look +Y

await client.PlayAsync();
await client.StepAsync(30); // let the renderer warm up

var frame = await client.Sensors.GetFrameAsync(cam);
var path = Path.GetFullPath("snapshot.png");
Png.Save(path, frame.Image);
Console.WriteLine($"Saved {frame.Image.Width}x{frame.Image.Height} -> {path}");
