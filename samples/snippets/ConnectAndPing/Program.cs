// ConnectAndPing — connect to a running bridge and check the handshake.
// Prereq: start the bridge first (bridge\run_bridge.bat), or see the RunHeadless sample.
using IsaacSimSharp;

using var client = IsaacSimClient.Connect(); // tcp://127.0.0.1:5599

Console.WriteLine($"ping  -> {await client.PingAsync("hello")}");
var v = await client.GetVersionAsync();
Console.WriteLine($"Isaac Sim {v.IsaacSimVersion} | bridge {v.BridgeVersion} | protocol {v.ProtocolVersion}");
