using IsaacSimSharp;

// Loads a Franka Panda from the Isaac asset library and drives its arm to a pose.
// Usage: dotnet run --project samples/RobotControl [tcp://host:port]
var endpoint = args.Length > 0 ? args[0] : IsaacSimClientOptions.DefaultCommandEndpoint;

using var client = IsaacSimClient.Connect(endpoint);
Console.WriteLine($"Connected to Isaac Sim {(await client.GetVersionAsync()).IsaacSimVersion}");

await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun");

var root = await client.GetAssetsRootAsync();
var frankaUsd = $"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd";
Console.WriteLine($"Loading Franka from {frankaUsd}");
await client.Scene.AddReferenceAsync(frankaUsd, "/World/robot");

// Start the sim and let the articulation initialize before reading DOFs.
await client.PlayAsync();
await client.StepAsync(5);

var robot = await client.Robots.RegisterAsync("/World/robot");
Console.WriteLine($"Articulation /World/robot has {robot.DofCount} DOFs: {string.Join(", ", robot.DofNames)}");

var before = await robot.GetStateAsync();
Console.WriteLine($"positions before: [{string.Join(", ", before.Positions.Select(p => p.ToString("F3")))}]");

// Drive the 7 arm joints to a target pose (fingers left as-is).
double[] armTarget = { 0.012, -0.568, 0.0, -2.811, 0.0, 3.037, 0.741 };
int[] armIndices = { 0, 1, 2, 3, 4, 5, 6 };
await robot.SetPositionTargetsAsync(armTarget, armIndices);

await client.StepAsync(90); // let the PD controller converge
var after = await robot.GetStateAsync();
Console.WriteLine($"positions after : [{string.Join(", ", after.Positions.Select(p => p.ToString("F3")))}]");

var maxArmError = armIndices.Max(i => Math.Abs(after.Positions[i] - armTarget[i]));
Console.WriteLine($"max arm joint error vs target: {maxArmError:F3} rad");
Console.WriteLine(maxArmError < 0.05 ? "Arm reached target. OK." : "Arm did NOT converge.");
