using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Imaging;

// Builds a scene, attaches an RTX camera, then pulls one frame on demand and
// grabs several from the push stream, saving PNGs to out/.
// Usage: dotnet run --project samples/SensorStream [tcp://host:port]
var endpoint = args.Length > 0 ? args[0] : IsaacSimClientOptions.DefaultCommandEndpoint;
var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "out"));
Directory.CreateDirectory(outDir);

using var client = IsaacSimClient.Connect(endpoint);
Console.WriteLine($"Connected to Isaac Sim {(await client.GetVersionAsync()).IsaacSimVersion}");

await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.Scene.AddPrimitiveAsync("/World/Box", IsaacSimSharp.Scene.PrimitiveShape.Cube,
    position: new Vector3(0, 0, 0.5f), size: 0.5);

// Camera at y=-3 looking toward +Y (the box), wxyz = (1,1,0,0)/sqrt2.
var q = 1.0f / MathF.Sqrt(2);
var cam = await client.Sensors.CreateCameraAsync(
    "/World/cam", width: 640, height: 480,
    position: new Vector3(0, -3f, 0.8f),
    orientation: new Quaternion(q, 0, 0, q), // (x,y,z,w)
    depth: true);
Console.WriteLine($"Created camera {cam}");

await client.PlayAsync();
await client.StepAsync(30); // let the renderer warm up

// --- Pull mode: one frame on demand ---
var pulled = await client.Sensors.GetFrameAsync(cam);
Console.WriteLine($"Pulled frame: {pulled.Image.Width}x{pulled.Image.Height} {pulled.Image.Encoding}, " +
                  $"{pulled.Image.Data.Length} bytes rgb, {pulled.Image.Depth.Length} bytes depth");
Png.Save(Path.Combine(outDir, "camera_pull.png"), pulled.Image);

// --- Push mode: subscribe and save the 5th streamed frame ---
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
var count = 0;
await foreach (var frame in client.Sensors.StreamAsync(cam, cts.Token))
{
    if (++count == 5)
    {
        Png.Save(Path.Combine(outDir, "camera_stream.png"), frame.Image);
        Console.WriteLine($"Saved streamed frame #{count} ({frame.Image.Width}x{frame.Image.Height})");
        break;
    }
}

Console.WriteLine($"Done. PNGs written to {outDir}");
