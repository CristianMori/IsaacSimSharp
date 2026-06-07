// DriveRobot — load a Franka and drive its arm joints to a target pose.
// Prereq: a running bridge (asset streams from the cloud library).
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun");

var root = await client.GetAssetsRootAsync();
await client.Scene.AddReferenceAsync($"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd", "/World/robot");
await client.PlayAsync();
await client.StepAsync(5);

var robot = await client.Robots.RegisterAsync("/World/robot");
Console.WriteLine($"{robot.DofCount} DOFs: {string.Join(", ", robot.DofNames)}");

double[] arm = { 0.0, -0.4, 0.0, -2.0, 0.0, 1.6, 0.79 };
await robot.SetPositionTargetsAsync(arm, new[] { 0, 1, 2, 3, 4, 5, 6 });
await client.StepAsync(90);

var state = await robot.GetStateAsync();
Console.WriteLine($"joint positions: [{string.Join(", ", state.Positions.Select(p => p.ToString("F2")))}]");
