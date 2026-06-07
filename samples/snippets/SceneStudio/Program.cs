// SceneStudio (orchestration) — self-launch Isaac Sim headless, compose a full scene
// (ground, lights, props, a robot), add a camera, simulate, save a snapshot, and export USD.
// Set %ISAACSIMSHARP_BRIDGE% or pass the bridge dir as arg 0.
using System.Numerics;
using IsaacSimSharp.Hosting;
using IsaacSimSharp.Imaging;
using IsaacSimSharp.Scene;

var bridgeDir = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("ISAACSIMSHARP_BRIDGE");
var outDir = Directory.CreateDirectory(Path.GetFullPath("studio_out")).FullName;

await using var session = await IsaacSimBridge.LaunchAsync(new BridgeLaunchOptions { BridgeDirectory = bridgeDir });
var client = session.Client;
Console.WriteLine($"Isaac Sim {(await client.GetVersionAsync()).IsaacSimVersion} launched.");

await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", LightKind.Distant, intensity: 1500);
await client.Scene.AddLightAsync("/World/Fill", LightKind.Dome, intensity: 400);

// scatter a few dynamic props in front of the robot
for (var i = 0; i < 4; i++)
{
    await client.Scene.AddPrimitiveAsync($"/World/Prop{i}",
        i % 2 == 0 ? PrimitiveShape.Cube : PrimitiveShape.Sphere,
        position: new Vector3(-0.5f + 0.35f * i, -0.6f, 0.6f + 0.2f * i),
        size: 0.16, collision: true, rigid: true);
}

// a robot
var root = await client.GetAssetsRootAsync();
await client.Scene.AddReferenceAsync($"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd", "/World/robot");

// a camera framing the robot + props (look +Y from in front, slightly above)
var q = 1.0f / MathF.Sqrt(2);
var cam = await client.Sensors.CreateCameraAsync("/World/cam", 800, 600,
    position: new Vector3(0, -2.2f, 0.85f), orientation: new Quaternion(q, 0, 0, q));

await client.PlayAsync();
await client.StepAsync(120); // props fall/settle, renderer warms up

Png.Save(Path.Combine(outDir, "studio.png"), (await client.Sensors.GetFrameAsync(cam)).Image);
var usd = await client.ExportUsdAsync(Path.Combine(outDir, "studio.usda"));
Console.WriteLine($"Saved snapshot + exported {usd}");
Console.WriteLine($"Prims on stage: {(await client.Usd.ListPrimsAsync()).Count}");
