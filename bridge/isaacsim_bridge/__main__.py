"""Entry point: launch Isaac Sim, then run the ZeroMQ bridge server.

Run via the bundled interpreter so the isaacsim/omni modules are importable:

    bridge\\run_bridge.bat
    # or
    set PYTHONPATH=<repo>\\bridge
    C:\\isaacsim\\python.bat -m isaacsim_bridge --command-endpoint tcp://127.0.0.1:5599
"""

import argparse

# IMPORTANT: SimulationApp must be created before importing any other isaacsim/omni module.
from isaacsim import SimulationApp


def main() -> None:
    parser = argparse.ArgumentParser(description="IsaacSimSharp bridge")
    parser.add_argument("--command-endpoint", default="tcp://127.0.0.1:5599")
    parser.add_argument("--sensor-endpoint", default="tcp://127.0.0.1:5600")
    parser.add_argument("--gui", action="store_true", help="show the Isaac Sim UI (default: headless)")
    parser.add_argument(
        "--motion-bvh",
        action="store_true",
        help="enable Motion BVH; required for RTX radar (Doppler), but slower and uses more VRAM",
    )
    args = parser.parse_args()

    sim_app = SimulationApp({"headless": not args.gui, "enable_motion_bvh": args.motion_bvh})

    # Safe to import the bridge server now that the Kit app exists.
    from isaacsim_bridge.server import BridgeServer

    server = BridgeServer(sim_app, args.command_endpoint, args.sensor_endpoint)
    server.run()


if __name__ == "__main__":
    main()
