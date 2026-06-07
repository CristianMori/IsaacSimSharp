"""Pure-Python mock of the Isaac Sim bridge.

Speaks the IsaacSimSharp ZeroMQ + Protobuf protocol WITHOUT launching Isaac Sim,
so the C# SDK, tests, and samples can run on any machine with no GPU. Returns
canned replies, and pushes canned sensor frames over the PUB socket for any
subscribed handles.

Run:  py -3.12 mock/mock_bridge.py
"""

import argparse
import os
import sys
import threading
import time

import zmq

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isaacsim_pb2 as pb  # noqa: E402  (generated from proto/isaacsim.proto)

PROTOCOL_VERSION = "0.1.0"
BRIDGE_VERSION = "mock-0.1.0"

_ACK_ONLY = {
    "new_stage", "open_stage", "play", "pause", "stop", "reset",
    "set_physics_dt", "shutdown", "set_prim_pose", "remove_prim",
    "subscribe", "unsubscribe",
}

_PRIM_OPS = {
    "add_ground_plane": "/World/GroundPlane",
    "add_light": "/World/Light",
    "add_primitive": "/World/Prim",
    "add_reference": "/World/Reference",
    "import_urdf": "/World/Urdf",
}


def make_frame(handle: str, state: dict) -> "pb.SensorFrame":
    import struct

    kind, width, height = state["sensors"][handle]
    frame = pb.SensorFrame(handle=handle, frame=state["frame"], sim_time=state["frame"] / 60.0)
    if kind == "camera":
        frame.type = pb.SENSOR_CAMERA
        frame.image.width = width
        frame.image.height = height
        frame.image.channels = 4
        frame.image.encoding = "rgba8"
        frame.image.data = bytes([128, 128, 128, 255]) * (width * height)
    elif kind == "contact":
        frame.type = pb.SENSOR_CONTACT
        frame.contact.in_contact = True
        frame.contact.force.z = 5.0
        frame.contact.count = 1
    elif kind == "lidar":
        frame.type = pb.SENSOR_LIDAR
        points = [(1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)]
        frame.point_cloud.count = len(points)
        frame.point_cloud.points = b"".join(struct.pack("<fff", *p) for p in points)
    else:
        frame.type = pb.SENSOR_IMU
        frame.imu.linear_acceleration.z = 9.81
        frame.imu.orientation.w = 1.0
    return frame


def handle(cmd: "pb.Command", state: dict) -> "pb.Reply":
    reply = pb.Reply(id=cmd.id, ok=True)
    which = cmd.WhichOneof("request")
    if which == "ping":
        reply.ping.message = cmd.ping.message
    elif which == "get_version":
        reply.get_version.isaac_sim_version = "mock"
        reply.get_version.bridge_version = BRIDGE_VERSION
        reply.get_version.protocol_version = PROTOCOL_VERSION
    elif which == "step":
        state["frame"] += cmd.step.count or 1
        reply.step.frame = state["frame"]
        reply.step.sim_time = state["frame"] / 60.0
    elif which == "export_usd":
        reply.export_usd.path = cmd.export_usd.path or "(mock).usda"
    elif which in _PRIM_OPS:
        reply.prim.prim_path = getattr(cmd, which).prim_path or _PRIM_OPS[which]
    elif which == "get_assets_root":
        reply.get_assets_root.path = "mock://assets"
    elif which == "register_articulation":
        path = cmd.register_articulation.prim_path
        reply.articulation.prim_path = path
        reply.articulation.dof_names.extend([f"joint{i}" for i in range(state["dofs"])])
        reply.articulation.dof_count = state["dofs"]
    elif which == "get_dof_state":
        n = state["dofs"]
        reply.dof_state.positions.extend(state["targets"] or [0.0] * n)
        reply.dof_state.velocities.extend([0.0] * n)
        reply.dof_state.efforts.extend([0.0] * n)
    elif which == "set_dof_targets":
        state["targets"] = list(cmd.set_dof_targets.values)
    elif which == "create_camera":
        h = cmd.create_camera.prim_path or "/World/camera"
        state["sensors"][h] = ("camera", cmd.create_camera.width or 8, cmd.create_camera.height or 8)
        reply.sensor.handle = h
    elif which == "create_imu":
        h = cmd.create_imu.prim_path or "/World/imu_sensor"
        state["sensors"][h] = ("imu", 0, 0)
        reply.sensor.handle = h
    elif which == "create_contact":
        h = cmd.create_contact.prim_path or "/World/contact_sensor"
        state["sensors"][h] = ("contact", 0, 0)
        reply.sensor.handle = h
    elif which == "create_lidar":
        h = cmd.create_lidar.prim_path or "/World/lidar"
        state["sensors"][h] = ("lidar", 0, 0)
        reply.sensor.handle = h
    elif which == "get_sensor_frame":
        h = cmd.get_sensor_frame.handle
        if h in state["sensors"]:
            reply.sensor_frame.CopyFrom(make_frame(h, state))
        else:
            reply.ok = False
            reply.error = f"unknown sensor '{h}'"
    elif which == "subscribe":
        h = cmd.subscribe.handle
        if h in state["sensors"]:
            state["subscriptions"].add(h)
        else:
            reply.ok = False
            reply.error = f"unknown sensor '{h}'"
    elif which == "unsubscribe":
        state["subscriptions"].discard(cmd.unsubscribe.handle)
    elif which in _ACK_ONLY:
        pass
    else:
        reply.ok = False
        reply.error = f"mock bridge: unhandled request '{which}'"
    return reply


def main() -> None:
    parser = argparse.ArgumentParser(description="IsaacSimSharp mock bridge")
    parser.add_argument("--command-endpoint", default="tcp://127.0.0.1:5599")
    parser.add_argument("--sensor-endpoint", default="tcp://127.0.0.1:5600")
    args = parser.parse_args()

    ctx = zmq.Context.instance()
    router = ctx.socket(zmq.ROUTER)
    router.bind(args.command_endpoint)
    pub = ctx.socket(zmq.PUB)
    pub.bind(args.sensor_endpoint)
    print(f"[mock-bridge] listening on {args.command_endpoint} (sensors on {args.sensor_endpoint})", flush=True)

    state = {"frame": 0, "dofs": 9, "targets": None, "sensors": {}, "subscriptions": set()}

    stop = threading.Event()

    def publisher() -> None:
        while not stop.is_set():
            for h in list(state["subscriptions"]):
                try:
                    pub.send_multipart([h.encode("utf-8"), make_frame(h, state).SerializeToString()])
                except Exception:  # noqa: BLE001
                    pass
            time.sleep(0.05)

    threading.Thread(target=publisher, daemon=True).start()

    try:
        while True:
            identity, payload = router.recv_multipart()
            cmd = pb.Command.FromString(payload)
            reply = handle(cmd, state)
            router.send_multipart([identity, reply.SerializeToString()])
    except KeyboardInterrupt:
        pass
    finally:
        stop.set()
        router.close(0)
        pub.close(0)
        ctx.term()


if __name__ == "__main__":
    main()
