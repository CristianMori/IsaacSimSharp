// DigitalTwinFeed (orchestration) — a runtime control loop: load a robot, then in a loop
// drive its arm, stream the camera, teleport a tracked prop, and raycast the scene.
// Showcases pushing/reading live state — the shape a digital-twin runtime would use.
// Set %ISAACSIMSHARP_BRIDGE% or pass the bridge dir as arg 0.
using System.Numerics;
using IsaacSimSharp.Hosting;
using IsaacSimSharp.Scene;

var gui = args.Contains("--gui"); // pass --gui to watch the arm move in the Isaac Sim window
var positional = args.Where(a => !a.StartsWith("--")).ToArray();
var bridgeDir = positional.Length > 0 ? positional[0] : Environment.GetEnvironmentVariable("ISAACSIMSHARP_BRIDGE");

await using var session = await IsaacSimBridge.LaunchAsync(new BridgeLaunchOptions { BridgeDirectory = bridgeDir, Gui = gui });
var client = session.Client;

await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);

var root = await client.GetAssetsRootAsync();
await client.Scene.AddReferenceAsync($"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd", "/World/robot");
var prop = await client.CreateCubeAsync("/World/Prop", position: new Vector3(0.5f, 0, 1), size: 0.15,
    collision: true, rigid: true);
var cam = await client.Sensors.CreateCameraAsync("/World/cam", 320, 240,
    position: new Vector3(1.6f, -1.8f, 1.1f), orientation: new Quaternion(0.5f, 0, 0, 0.5f));

await client.PlayAsync();
await client.StepAsync(5);
var robot = await client.Robots.RegisterAsync("/World/robot");
int[] arm = { 0, 1, 2, 3, 4, 5, 6 };

double[][] waypoints =
[
    [0.0, -0.4, 0.0, -2.0, 0.0, 1.6, 0.79],
    [1.0, -0.2, 0.3, -1.5, 0.0, 1.4, 0.79],
    [-1.0, -0.8, -0.4, -2.4, 0.4, 2.2, 0.2],
];

var passes = gui ? 3 : 1; // loop a few times when watching, so the arm motion is sustained
for (var pass = 0; pass < passes; pass++)
{
    for (var step = 0; step < waypoints.Length; step++)
    {
        await robot.SetPositionTargetsAsync(waypoints[step], arm);          // drive
        await prop.SetWorldPoseAsync(new Vector3(0.5f, 0, 1.0f + step), Quaternion.Identity); // teleport state in
        await client.StepAsync(60);

        var frame = await client.Sensors.GetFrameAsync(cam);               // read perception
        var hit = await client.Physics.RaycastAsync(new Vector3(0.5f, 0, 5), new Vector3(0, 0, -1), 100); // query world
        var js = await robot.GetStateAsync();
        Console.WriteLine($"pass{pass} wp{step}: cam {frame.Image.Width}x{frame.Image.Height} | " +
                          $"ray->{(hit.Hit ? hit.PrimPath : "miss")}@{hit.Distance:F2} | j0={js.Positions[0]:F2}");
    }
}

Console.WriteLine("Digital-twin feed loop complete.");
