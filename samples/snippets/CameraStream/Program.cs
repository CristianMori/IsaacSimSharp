// CameraStream — subscribe to a camera and consume pushed frames as an IAsyncEnumerable.
// Prereq: a running bridge.
using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Scene;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube, new Vector3(0, 0, 0.5f), size: 0.5);

var q = 1.0f / MathF.Sqrt(2);
var cam = await client.Sensors.CreateCameraAsync("/World/cam", 320, 240,
    position: new Vector3(0, -3f, 0.8f), orientation: new Quaternion(q, 0, 0, q));
await client.PlayAsync();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
var n = 0;
await foreach (var frame in client.Sensors.StreamAsync(cam, cts.Token))
{
    Console.WriteLine($"frame {++n}: {frame.Image.Width}x{frame.Image.Height} @ sim {frame.SimTime:F2}s");
    if (n >= 10) break; // unsubscribes automatically on exit
}
