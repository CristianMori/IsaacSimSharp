# IsaacSimSharp samples

Open **`IsaacSimSharp.Samples.slnx`**. Samples come in two flavors:

- **Focused snippets** (`snippets/`) — one capability each, kept tiny on purpose. A shared
  `snippets/Directory.Build.props` gives every snippet the SDK reference, so each project is
  just its `Program.cs`. Most **connect to an already-running bridge** — start it first:
  ```
  bridge\run_bridge.bat            (add --gui to watch, --motion-bvh for radar, --livestream to stream)
  ```
- **Self-launching** samples start and stop Isaac Sim themselves via `IsaacSimBridge.LaunchAsync`
  (point them at the bridge folder with `%ISAACSIMSHARP_BRIDGE%` or arg 0): `RunHeadless`,
  `RunWithUi`, `LivestreamHeadless`, `SceneStudio`, `DigitalTwinFeed`, and the top-level
  `SelfHosted` sample.

Run any sample with: `dotnet run --project samples/snippets/<Name>`

## Snippets by area

| Hosting | Scene / USD | Manipulation | Physics | Sensors | Robot |
|---|---|---|---|---|---|
| ConnectAndPing | BuildScene | EditScope | PhysicsAuthoring | CameraSnapshot | LoadRobot |
| RunHeadless | CreateAnyPrim | ReparentRenameDuplicate | Materials | CameraStream | DriveRobot |
| RunWithUi | EnumerateStage | VisibilityAndActive | RuntimePhysics | ImuSensor | |
| LivestreamHeadless | ReadWriteAttributes | TransformsAndBounds | Raycast | LidarSensor | |
| | ExportUsd | | | | |

## Bigger showcases

- **SceneStudio** — compose a full scene (ground, lights, props, a robot), simulate, save a
  camera snapshot, and export USD. (self-launching)
- **DigitalTwinFeed** — a runtime control loop: drive the robot, stream the camera, teleport a
  tracked prop, and raycast the scene each step — the shape a digital-twin runtime would use.
  (self-launching)
- **Quickstart / RobotControl / SensorStream / SelfHosted** — the original end-to-end samples.
