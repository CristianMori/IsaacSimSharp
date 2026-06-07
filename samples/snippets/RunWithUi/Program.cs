// RunWithUi — launch Isaac Sim with the editor window so you can watch.
using IsaacSimSharp.Hosting;

var bridgeDir = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("ISAACSIMSHARP_BRIDGE");

await using var session = await IsaacSimBridge.LaunchAsync(new BridgeLaunchOptions
{
    BridgeDirectory = bridgeDir,
    Gui = true, // opens the Isaac Sim window
});

var client = session.Client;
Console.WriteLine("Window open. Building a scene you can watch...");
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.PlayAsync();
await client.StepAsync(180);
Console.WriteLine("Done. (Dispose stops the sim; close the window to end it too.)");
