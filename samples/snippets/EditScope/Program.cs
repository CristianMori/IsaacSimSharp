// EditScope — the object/handle paradigm: mutate a snapshot locally, flush in one round-trip.
// Prereq: a running bridge.
using IsaacSimSharp;

using var client = IsaacSimClient.Connect();
await client.NewStageAsync();
var cube = await client.CreateCubeAsync("/World/Box", size: 1.0);
await cube.SetPositionAsync(0, 0, 0);

// EditAsync loads the current transform; mutations are local until the scope ends.
await using (var e = await cube.EditAsync())
{
    e.Position.Y += 4;     // fluent, no per-setter network I/O
    e.Size.Width = 30;     // (cube size maps to scale)
    e.Size.Depth = 5;
}                          // one SetTransform on dispose

var t = await cube.GetTransformAsync();
Console.WriteLine($"after edit: Y={t.Translation.Y}, scale.X={t.Scale.X}, scale.Z={t.Scale.Z}");
