// LivestreamHeadless — launch a headless sim with WebRTC livestream, then keep it
// running so you can connect the Isaac Sim WebRTC Streaming Client to localhost:49100.
using IsaacSimSharp.Hosting;

var bridgeDir = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("ISAACSIMSHARP_BRIDGE");

await using var session = await IsaacSimBridge.LaunchAsync(new BridgeLaunchOptions
{
    BridgeDirectory = bridgeDir,
    Livestream = true, // WebRTC signaling on :49100
});

var client = session.Client;
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);
await client.PlayAsync();

Console.WriteLine("Streaming on localhost:49100. Connect the Isaac Sim WebRTC client. Ctrl+C to stop.");
while (true)
{
    await client.StepAsync(60);
}
