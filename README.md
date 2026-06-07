# IsaacSimSharp

A typed **C# API for NVIDIA Isaac Sim 6.0**. It lets a .NET application drive Isaac Sim in
realtime — build a scene, control robots, read sensors, and export USD — by talking to a small
Python bridge that runs *inside* the simulator over **ZeroMQ + Protobuf**.

```csharp
using var client = IsaacSimClient.Connect();              // tcp://127.0.0.1:5599
await client.NewStageAsync();
await client.Scene.AddGroundPlaneAsync();
await client.Scene.AddLightAsync("/World/Sun", intensity: 1500);

var root = await client.GetAssetsRootAsync();
await client.Scene.AddReferenceAsync($"{root}/Isaac/Robots/FrankaRobotics/FrankaPanda/franka.usd", "/World/robot");
await client.PlayAsync();
await client.StepAsync(5);

var robot = await client.Robots.RegisterAsync("/World/robot");
await robot.SetPositionTargetsAsync(new double[] { 0.0, -0.57, 0.0, -2.81, 0.0, 3.04, 0.74 },
                                    new[] { 0, 1, 2, 3, 4, 5, 6 });

var cam = await client.Sensors.CreateCameraAsync("/World/cam", 640, 480);
await foreach (var frame in client.Sensors.StreamAsync(cam))
    Process(frame.Image);                                 // RGB8 (+ optional float32 depth)

await client.ExportUsdAsync(@"C:\out\scene.usda");        // hand-off artifact for a USD runtime
```

## Why this design

Isaac Sim's only supported scripting surface is **Python**, running in-process inside Omniverse
Kit. So a Python **bridge** runs inside the sim and exposes a network API; the C# library is the
client. (Python speed is a non-issue — it's a thin shell over C++/CUDA/PhysX; the per-step cost is
the same from any language. The boundary cost is minimized with binary Protobuf and a poll-in-the-
step-loop design.)

```
   your C# app  ──►  IsaacSimSharp (this SDK)
                          │  ZeroMQ + Protobuf
                          │   • command socket  (ROUTER/DEALER, request/reply)
                          │   • sensor socket   (PUB/SUB, streamed frames)
                          ▼
                  isaacsim_bridge  (Python, runs INSIDE Isaac Sim)
                          │  in-process calls
                          ▼
                  Isaac Sim 6.0   (PhysX, RTX, USD, robots, sensors)
```

`proto/isaacsim.proto` is the single source of truth: it generates the C# message classes (at
build, via Grpc.Tools) and the Python classes (via `tools/gen_proto.ps1`).

## Layout

| Path | What |
|---|---|
| `proto/isaacsim.proto` | wire protocol (commands + sensor frames); C# classes are generated into the SDK at build |
| `src/IsaacSimSharp/` | the C# SDK (`IsaacSimClient` + `Scene` / `Robots` / `Sensors` facades) |
| `bridge/isaacsim_bridge/` | Python bridge that runs inside Isaac Sim |
| `mock/mock_bridge.py` | pure-Python mock (no GPU) for dev, tests, CI |
| `samples/` | `Quickstart` (scene + export), `RobotControl`, `SensorStream` (camera → PNG) |
| `tests/` | xUnit tests run against the mock |
| `tools/gen_proto.ps1` | regenerate the Python protobuf classes |

## Prerequisites

- **.NET 10 SDK**
- **NVIDIA Isaac Sim 6.0** (RTX GPU) for live use — default location `C:\isaacsim`
  (override with `ISAACSIM_HOME`). Not needed to build the SDK or run the mock-based tests.
- **Python 3.12** with `pyzmq` + `protobuf` for the mock (`py -3.12 -m pip install pyzmq protobuf`).

## Setup

```powershell
# 1. Install the bridge's Python deps into Isaac Sim's bundled interpreter
C:\isaacsim\python.bat -m pip install -r bridge\requirements.txt

# 2. Build the C# solution (generates C# protobuf classes automatically)
dotnet build

# 3. (Only after editing proto/isaacsim.proto) regenerate the Python classes
pwsh tools\gen_proto.ps1
```

## Running

**Start the bridge inside Isaac Sim** (headless by default; first launch is slow while shaders
cache, then it prints `listening on ...`):

```powershell
bridge\run_bridge.bat --command-endpoint tcp://127.0.0.1:5599 --sensor-endpoint tcp://127.0.0.1:5600
# add --gui to show the Isaac Sim window
# add --motion-bvh to enable RTX radar (slower; off by default)
```

**Then run a sample:**

```powershell
dotnet run --project samples/Quickstart      # build a scene, simulate, export USD
dotnet run --project samples/RobotControl    # load a Franka and drive its arm to a pose
dotnet run --project samples/SensorStream    # stream an RTX camera and save PNGs to out/
```

**No GPU?** Develop and test against the mock:

```powershell
py -3.12 mock\mock_bridge.py                  # in one terminal
dotnet test                                   # in another
```

## API surface

- **Lifecycle** (`client`): `NewStageAsync`, `OpenStageAsync`, `PlayAsync`, `PauseAsync`,
  `StopAsync`, `ResetAsync`, `StepAsync`, `SetPhysicsDtAsync`, `ExportUsdAsync`, `ShutdownAsync`,
  `GetAssetsRootAsync`.
- **Scene** (`client.Scene`): `AddGroundPlaneAsync`, `AddLightAsync`, `AddPrimitiveAsync`,
  `AddReferenceAsync`, `ImportUrdfAsync`, `SetPrimPoseAsync`, `RemovePrimAsync`.
- **Robots** (`client.Robots`): `RegisterAsync` → `RobotArticulation` with `GetStateAsync`,
  `SetPositionTargetsAsync`, `SetVelocityTargetsAsync`, `SetEffortsAsync`.
- **Sensors** (`client.Sensors`): `CreateCameraAsync`, `CreateImuAsync`, `CreateContactAsync`,
  `CreateLidarAsync`, `CreateRadarAsync` (needs `--motion-bvh`), `GetFrameAsync` (pull),
  `StreamAsync` (push, `IAsyncEnumerable`).

## Status

| Milestone | Verified against Isaac Sim 6.0 |
|---|---|
| Scaffold + ZeroMQ handshake | mock |
| Lifecycle + USD export | export + reopen |
| Scene configuration | ground/light/primitives fall under gravity; pose/remove |
| Robot control | Franka loaded, 9 DOFs, arm drives to target |
| Sensors (pull + push) | RTX camera (RGB8 + depth), contact, and lidar (point cloud) |

All four sensor types (camera, contact, lidar, IMU) and `ImportUrdfAsync` are verified live
(camera RGB8+depth; contact `in_contact`/count/force-magnitude; lidar ~200k-point cloud with
per-point intensity, decoded from the GMO buffer; URDF import of `assets/urdf/04-materials.urdf`
→ `/World/robot`). IMU uses best-effort field extraction. RTX **radar** works only when the
bridge is launched with `--motion-bvh` (Doppler needs Motion BVH; without it `CreateRadarAsync`
returns a clear error instead of crashing the sim). Motion BVH is off by default because it
slows all sensors and uses more VRAM.

The SDK packs as a single self-contained NuGet package (`dotnet pack src/IsaacSimSharp`),
depending only on `NetMQ` and `Google.Protobuf`.
