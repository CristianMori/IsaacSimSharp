using System.Numerics;
using IsaacSimSharp;
using IsaacSimSharp.Imaging;
using IsaacSimSharp.Scene;

// Loads a Franka Panda and sweeps its arm through several poses, then saves a camera frame.
// Usage: dotnet run --project samples/RobotControl [tcp://host:port]
var endpoint = args.Length > 0 ? args[0] : IsaacSimClientOptions.DefaultCommandEndpoint;
var outDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "out"));
Directory.CreateDirectory(outDir);

using var client = IsaacSimClient.Connect(endpoint);
Console.WriteLine($"Connected to Isaac Sim {(await client.GetVersionAsync()).IsaacSimVersion}");

await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);

var root = await client.GetAssetsRootAsync();
await client.Scene.AddReferenceAsync($"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd", "/World/robot");

// A camera looking at the robot (wxyz = (1,1,0,0)/sqrt2 → looks +Y, up +Z).
var q = 1.0f / MathF.Sqrt(2);
var cam = await client.Sensors.CreateCameraAsync(
    "/World/cam", width: 640, height: 480,
    position: new Vector3(0f, -2.4f, 0.9f), // on the robot's axis, looking +Y at it
    orientation: new Quaternion(q, 0, 0, q),
    depth: false);

await client.PlayAsync();
await client.StepAsync(5);

var robot = await client.Robots.RegisterAsync("/World/robot");
Console.WriteLine($"Franka ready: {robot.DofCount} DOFs");

int[] arm = { 0, 1, 2, 3, 4, 5, 6 };
var poses = new (string Name, double[] Joints)[]
{
    ("home",  new[] { 0.0, -0.40, 0.0, -2.0, 0.0, 1.6, 0.79 }),
    ("right", new[] { 1.2, -0.20, 0.3, -1.5, 0.0, 1.4, 0.79 }),
    ("left",  new[] { -1.2, -0.80, -0.4, -2.4, 0.4, 2.2, 0.20 }),
    ("reach", new[] { 0.0, 0.30, 0.0, -1.2, 0.0, 1.6, 0.79 }),
    ("home",  new[] { 0.0, -0.40, 0.0, -2.0, 0.0, 1.6, 0.79 }),
};

foreach (var (name, joints) in poses)
{
    Console.WriteLine($"Moving arm to '{name}' ...");
    await robot.SetPositionTargetsAsync(joints, arm);
    await client.StepAsync(120); // let the PD controller drive there (watch the window)
}

var frame = await client.Sensors.GetFrameAsync(cam);
var pngPath = Path.Combine(outDir, "robot_arm.png");
Png.Save(pngPath, frame.Image);
Console.WriteLine($"Saved a view of the arm -> {pngPath}");
Console.WriteLine("Done.");
