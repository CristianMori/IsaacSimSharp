using System.Numerics;
using IsaacSimSharp.Hosting;
using IsaacSimSharp.Scene;

// Launches Isaac Sim + the bridge entirely from C#, builds a scene, then shuts it down.
// Usage: dotnet run --project samples/SelfHosted [bridgeDir]
var bridgeDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "bridge"));

Console.WriteLine($"Launching Isaac Sim bridge from {bridgeDir} (this can take a minute on first run) ...");

await using var session = await IsaacSimBridge.LaunchAsync(new BridgeLaunchOptions
{
    BridgeDirectory = bridgeDir,
    Gui = true, // open the Isaac Sim window so you can watch
});

var client = session.Client;
Console.WriteLine($"Bridge ready. Isaac Sim {(await client.GetVersionAsync()).IsaacSimVersion}");

await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.Scene.AddPrimitiveAsync("/World/Box", PrimitiveShape.Cube,
    position: new Vector3(0, 0, 1.5f), size: 0.3, collision: true, rigid: true);

Console.WriteLine("Playing 150 frames (watch the box fall) ...");
await client.PlayAsync();
await client.StepAsync(150);

Console.WriteLine("Done. Disposing the session shuts Isaac Sim down.");
