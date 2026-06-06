using IsaacSimSharp;

// Drives a running bridge (mock or real Isaac Sim) through the lifecycle and exports USD.
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

Console.WriteLine("Creating a new stage ...");
await client.NewStageAsync();

try
{
    await client.SetPhysicsDtAsync(1.0 / 60.0);
    Console.WriteLine("Physics dt set to 1/60.");
}
catch (IsaacSimException ex)
{
    Console.WriteLine($"(SetPhysicsDt skipped: {ex.Message})");
}

Console.WriteLine("Playing + stepping 60 frames ...");
await client.PlayAsync();
var step = await client.StepAsync(60);
Console.WriteLine($"Stepped to frame {step.Frame}, sim_time {step.SimTime:F3}s");
await client.PauseAsync();

Console.WriteLine($"Exporting USD -> {exportPath}");
var written = await client.ExportUsdAsync(exportPath);
Console.WriteLine($"Exported: {written}");

Console.WriteLine("Done.");
