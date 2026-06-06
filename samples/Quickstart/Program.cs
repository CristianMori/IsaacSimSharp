using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Scene;

// Builds a scene in a running bridge (mock or real Isaac Sim) and exports it to USD.
// Usage: dotnet run --project samples/Quickstart [tcp://host:port] [exportPath]
var endpoint = args.Length > 0 ? args[0] : IsaacSimClientOptions.DefaultCommandEndpoint;
var exportPath = args.Length > 1
    ? args[1]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "out", "quickstart.usda");
exportPath = Path.GetFullPath(exportPath);

Console.WriteLine($"Connecting to Isaac Sim bridge at {endpoint} ...");
using var client = IsaacSimClient.Connect(endpoint);

Console.WriteLine($"Ping -> {await client.PingAsync("hello isaac")}");
var version = await client.GetVersionAsync();
Console.WriteLine($"Isaac Sim {version.IsaacSimVersion} | bridge {version.BridgeVersion} | protocol {version.ProtocolVersion}");

Console.WriteLine("Building scene ...");
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);

Console.WriteLine($"  + {await client.Scene.AddGroundPlaneAsync()}");
Console.WriteLine($"  + {await client.Scene.AddLightAsync("/World/Sun", LightKind.Distant, intensity: 1500)}");
Console.WriteLine($"  + {await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube,
    position: new Vector3(0, 0, 1.0f), size: 0.3, collision: true, rigid: true)}");
Console.WriteLine($"  + {await client.Scene.AddPrimitiveAsync("/World/Ball", PrimitiveShape.Sphere,
    position: new Vector3(0.4f, 0, 2.0f), size: 0.25, collision: true, rigid: true)}");

Console.WriteLine("Playing + stepping 60 frames (objects fall under gravity) ...");
await client.PlayAsync();
var step = await client.StepAsync(60);
Console.WriteLine($"Stepped to frame {step.Frame}, sim_time {step.SimTime:F3}s");
await client.PauseAsync();

Console.WriteLine($"Exporting USD -> {exportPath}");
var written = await client.ExportUsdAsync(exportPath);
Console.WriteLine($"Exported: {written}");

Console.WriteLine("Done.");
