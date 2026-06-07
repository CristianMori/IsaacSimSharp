# IsaacSimSharp samples

Open **`IsaacSimSharp.Samples.slnx`**. Run any sample with:

```powershell
dotnet run --project samples/snippets/<Name>
# original samples live one level up:
dotnet run --project samples/<Name>
```

## Two run modes

- **Connect to a running bridge** (most snippets). Start the bridge first:
  ```powershell
  bridge\run_bridge.bat        # --gui to watch, --motion-bvh for radar, --livestream to stream
  ```
- **Self-launching** samples start/stop Isaac Sim themselves via `IsaacSimBridge.LaunchAsync`.
  Point them at the bridge folder with `%ISAACSIMSHARP_BRIDGE%` or pass it as the first argument:
  `RunHeadless`, `RunWithUi`, `LivestreamHeadless`, `SceneStudio`, `DigitalTwinFeed`, `SelfHosted`.
  The orchestration ones also accept `--gui` to open the window.

The focused snippets share `snippets/Directory.Build.props` (which supplies the SDK reference), so
each snippet project is nothing but its `Program.cs`.

---

## Hosting & connection

### ConnectAndPing
The smallest sample: connect to a running bridge, `PingAsync`, and print `GetVersionAsync`
(Isaac Sim / bridge / protocol versions). *Connect mode.*

### RunHeadless
Launch Isaac Sim headless from C# with `IsaacSimBridge.LaunchAsync`, create a stage, play, step,
and shut down on dispose. *Self-launch.*

### RunWithUi
Same as RunHeadless but `Gui = true`, so the Isaac Sim editor window opens and you watch a ground
plane + light get added and stepped. *Self-launch.*

### LivestreamHeadless
Launch headless with `Livestream = true` and keep stepping forever, so you can connect the **Isaac
Sim WebRTC Streaming Client to `localhost:49100`** and view the headless sim. *Self-launch.*

## Scene & USD

### BuildScene
Compose a scene with `client.Scene`: ground plane, distant light, and a cube + sphere as dynamic
rigid bodies; play and step so they fall and settle. *Connect mode.*

### CreateAnyPrim
Instantiate prims of arbitrary USD types with `Usd.DefinePrimAsync` — `Xform`, `Sphere`, `Mesh`,
`SphereLight`, `Scope`. Shows the generic (non-typed-helper) creation path. *Connect mode.*

### EnumerateStage
Reflective browsing: `Usd.ListPrimsAsync` (whole subtree), `Usd.FindPrimsAsync(typeName: "Cube")`,
and `Usd.GetPrimAsync` to inspect a prim's type / active / visibility / attribute count / applied
API schemas. *Connect mode.*

### ReadWriteAttributes
Generic attribute I/O with the typed convenience setters (`string`, `long`, `double`, `Vector3`)
followed by `GetAttributeAsync`, printing each value's USD type and `UsdValue` kind. *Connect mode.*

### TransformsAndBounds
Set a prim's position and (non-uniform) scale, read them back with `GetTransformAsync`, and query
the world-space axis-aligned bounding box with `GetBoundsAsync`. *Connect mode.*

### ExportUsd
Build a small scene and write it to a `.usda` file with `ExportUsdAsync` — the hand-off artifact a
downstream USD runtime would consume. *Connect mode.*

## Manipulation

### EditScope
The object/handle paradigm: `await cube.EditAsync()` loads the current transform, you mutate it
fluently (`e.Position.Y += 4`, `e.Size.Width = 30`) with no per-setter I/O, and the scope flushes
everything in **one** round-trip on dispose. *Connect mode.*

### ReparentRenameDuplicate
Structural edits via handle methods: `RenameAsync`, `ReparentAsync`, `DuplicateAsync` (each returns
a handle to the new path), confirmed with `FindPrimsAsync`. *Connect mode.*

### VisibilityAndActive
Toggle a prim's visibility (`SetVisibleAsync`) and activation (`SetActiveAsync`), and read the
state back from `DescribeAsync`. *Connect mode.*

## Physics

### PhysicsAuthoring
Turn a plain cube into a dynamic body: `ApplyColliderAsync`, `ApplyRigidBodyAsync`, `SetMassAsync`,
then play — it falls from z=3 and settles on the ground. *Connect mode.*

### Materials
Create a `UsdPreviewSurface` material with `Usd.CreateMaterialAsync(color, metallic, roughness)`
and bind it to a prim with `BindMaterialAsync`. *Connect mode.*

### RuntimePhysics
Runtime rigid-body state during simulation: `SetVelocityAsync` / `GetVelocityAsync`, and teleport
with `SetWorldPoseAsync` mid-sim. *Connect mode.*

### Raycast
Closest-hit scene query with `Physics.RaycastAsync(origin, direction, maxDistance)` against a box,
printing the hit prim, point, and distance. *Connect mode.*

## Sensors

### CameraSnapshot
Attach an RTX camera, `PlayAsync` + warm up, pull one frame with `GetFrameAsync`, and save it to a
PNG using the dependency-free `IsaacSimSharp.Imaging.Png`. *Connect mode.*

### CameraStream
Subscribe to a camera and consume pushed frames as an `IAsyncEnumerable` via `StreamAsync`; it
takes 10 frames then exits (auto-unsubscribes). *Connect mode.*

### ImuSensor
Attach an IMU to a rigid body with `CreateImuAsync`, step, and read linear acceleration from the
pulled `ImuFrame`. *Connect mode.*

### LidarSensor
Attach an RTX lidar, step until the rotary scan buffer fills, and read the Cartesian point cloud
(count + byte length) from the `PointCloudFrame`. *Connect mode.*

## Robot

### LoadRobot
Resolve the cloud asset library with `GetAssetsRootAsync` and reference a Franka Panda onto the
stage with `AddReferenceAsync`; register it and print its DOF names. *Connect mode (streams asset).*

### DriveRobot
Register the Franka articulation and drive its 7 arm joints to a target pose with
`SetPositionTargetsAsync`, then read joint positions with `GetStateAsync`. *Connect mode.*

## Orchestration showcases

### SceneStudio *(self-launch, `--gui` optional)*
Composes a full scene — ground, key + dome lights, four dynamic props, and a Franka — adds a
camera, simulates, saves a snapshot PNG, and exports a USD. Demonstrates hosting + scene authoring
+ assets + sensors + lifecycle + export in one program. Run `... -- <bridgeDir> --gui` to watch.

### DigitalTwinFeed *(self-launch, `--gui` optional)*
A runtime control loop: each step it drives the robot arm through waypoints, teleports a tracked
prop (`SetWorldPoseAsync`), reads the camera, and raycasts the scene — the shape a digital-twin
runtime would use to push state in and read perception out. With `--gui` the waypoint sweep loops a
few passes so the arm motion is sustained.

## Original end-to-end samples (`samples/`)

- **Quickstart** — connect, build a scene, simulate, export USD.
- **RobotControl** — load a Franka and sweep the arm through several poses, saving a camera view.
- **SensorStream** — pull one camera frame and stream several, saving PNGs.
- **SelfHosted** — launch Isaac Sim from C# (`IsaacSimBridge.LaunchAsync`), build a scene, tear down.
