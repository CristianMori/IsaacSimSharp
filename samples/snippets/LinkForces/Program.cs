// LinkForces — read the sensed 6D reaction force/torque at each of a robot's joints.
// These are measured loads (force feedback), distinct from the commanded efforts
// reported by GetStateAsync.
// Prereq: a running bridge (asset streams from the cloud library).
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
await client.SetPhysicsDtAsync(1.0 / 60.0);
await client.Scene.AddGroundPlaneAsync();

var root = await client.GetAssetsRootAsync();
await client.Scene.AddReferenceAsync($"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd", "/World/robot");
var robot = await client.Robots.RegisterAsync("/World/robot");

// Physics must be running for the tensor API to report joint reaction loads.
await client.PlayAsync();
await client.StepAsync(60); // let the arm settle under gravity

var forces = await robot.GetLinkForcesAsync();
Console.WriteLine($"{forces.LinkNames.Count} links:");
for (var i = 0; i < forces.LinkNames.Count; i++)
{
    var f = forces.Forces[i];
    var t = forces.Torques[i];
    Console.WriteLine($"  {forces.LinkNames[i],-18} |F|={f.Length(),7:F2} N   |T|={t.Length(),7:F2} N*m");
}
