// RunHeadless — launch Isaac Sim headless from C#, step, and shut down.
// Set %ISAACSIMSHARP_BRIDGE% to the repo's bridge folder, or pass it as arg 0.
using IsaacSimSharp.Hosting;

var bridgeDir = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("ISAACSIMSHARP_BRIDGE");

await using var session = await IsaacSimBridge.LaunchAsync(new BridgeLaunchOptions
{
    BridgeDirectory = bridgeDir, // null => uses %ISAACSIMSHARP_BRIDGE%
    // headless is the default
});

var client = session.Client;
Console.WriteLine($"Launched Isaac Sim {(await client.GetVersionAsync()).IsaacSimVersion} (headless)");
await client.NewStageAsync();
await client.PlayAsync();
var step = await client.StepAsync(60);
Console.WriteLine($"Stepped to frame {step.Frame} ({step.SimTime:F2}s). Shutting down.");
// disposing the session stops Isaac Sim
