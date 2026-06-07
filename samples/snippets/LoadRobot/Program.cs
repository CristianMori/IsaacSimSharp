// LoadRobot — resolve the asset library and reference a Franka onto the stage.
// Prereq: a running bridge (asset streams from the cloud library).
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun");

var root = await client.GetAssetsRootAsync();
Console.WriteLine($"assets root: {root}");
var prim = await client.Scene.AddReferenceAsync(
    $"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd", "/World/robot");
Console.WriteLine($"loaded robot at {prim}");

await client.PlayAsync();
await client.StepAsync(5);
var robot = await client.Robots.RegisterAsync("/World/robot");
Console.WriteLine($"{robot.DofCount} DOFs: {string.Join(", ", robot.DofNames)}");
