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
    "open_stage", "play", "pause", "stop", "reset",
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

_PRIM_TYPES = {
    "add_ground_plane": "Plane",
    "add_light": "DistantLight",
    "add_primitive": "Cube",
    "add_reference": "Xform",
    "import_urdf": "Xform",
}


def _identity_xform():
    return {"t": [0.0, 0.0, 0.0], "o": [1.0, 0.0, 0.0, 0.0], "s": [1.0, 1.0, 1.0]}


def make_frame(handle: str, state: dict) -> "pb.SensorFrame":
    import struct

    entry = state["sensors"][handle]
    kind, width, height = entry[0], entry[1], entry[2]
    seg = entry[3] if len(entry) > 3 else False
    inst = entry[4] if len(entry) > 4 else False
    frame = pb.SensorFrame(handle=handle, frame=state["frame"], sim_time=state["frame"] / 60.0)
    if kind == "camera":
        frame.type = pb.SENSOR_CAMERA
        frame.image.width = width
        frame.image.height = height
        frame.image.channels = 4
        frame.image.encoding = "rgba8"
        frame.image.data = bytes([128, 128, 128, 255]) * (width * height)
        if seg:
            frame.image.segmentation = bytes(width * height * 4)  # uint32 zeros
            frame.image.segmentation_labels[0] = "background"
        if inst:
            frame.image.instance_segmentation = bytes(width * height * 4)
            frame.image.instance_labels[0] = "/World/unlabelled"
    elif kind == "contact":
        frame.type = pb.SENSOR_CONTACT
        frame.contact.in_contact = True
        frame.contact.force.z = 5.0
        frame.contact.force_magnitude = 5.0
        frame.contact.count = 1
    elif kind in ("lidar", "radar"):
        frame.type = pb.SENSOR_LIDAR if kind == "lidar" else pb.SENSOR_RADAR
        points = [(1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)]
        frame.point_cloud.count = len(points)
        frame.point_cloud.points = b"".join(struct.pack("<fff", *p) for p in points)
        frame.point_cloud.intensities = b"".join(struct.pack("<f", v) for v in (0.5, 0.7, 0.9))
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
        path = getattr(cmd, which).prim_path or _PRIM_OPS[which]
        reply.prim.prim_path = path
        entry = {"type": _PRIM_TYPES[which], "attrs": {}, "xform": _identity_xform()}
        if which == "add_primitive":
            p = cmd.add_primitive.position
            entry["xform"]["t"] = [p.x, p.y, p.z]
            size = cmd.add_primitive.size or 0.5
            entry["xform"]["s"] = [size, size, size]
        state["stage"][path] = entry
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
        state["sensors"][h] = ("camera", cmd.create_camera.width or 8, cmd.create_camera.height or 8,
                               cmd.create_camera.segmentation, cmd.create_camera.instance_segmentation)
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
    elif which == "create_radar":
        h = cmd.create_radar.prim_path or "/World/radar"
        state["sensors"][h] = ("radar", 0, 0)
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
    elif which == "new_stage":
        state["stage"] = {}
    elif which == "define_prim":
        p = cmd.define_prim.prim_path
        state["stage"][p] = {"type": cmd.define_prim.type_name, "attrs": {}, "xform": _identity_xform()}
        reply.prim.prim_path = p
    elif which == "get_transform":
        xf = state["stage"].get(cmd.get_transform.prim_path, {}).get("xform", _identity_xform())
        t = reply.transform.transform
        t.translation.x, t.translation.y, t.translation.z = xf["t"]
        t.orientation.w, t.orientation.x, t.orientation.y, t.orientation.z = xf["o"]
        t.scale.x, t.scale.y, t.scale.z = xf["s"]
    elif which == "set_transform":
        req = cmd.set_transform
        entry = state["stage"].setdefault(req.prim_path, {"type": "", "attrs": {}, "xform": _identity_xform()})
        xf = entry.setdefault("xform", _identity_xform())
        if req.HasField("translation"):
            xf["t"] = [req.translation.x, req.translation.y, req.translation.z]
        if req.HasField("orientation"):
            xf["o"] = [req.orientation.w, req.orientation.x, req.orientation.y, req.orientation.z]
        if req.HasField("scale"):
            xf["s"] = [req.scale.x, req.scale.y, req.scale.z]
    elif which == "get_bounds":
        entry = state["stage"].get(cmd.get_bounds.prim_path)
        if entry is None:
            reply.bounds.valid = False
        else:
            xf = entry.get("xform", _identity_xform())
            t, s = xf["t"], xf["s"]
            reply.bounds.valid = True
            reply.bounds.min.x, reply.bounds.min.y, reply.bounds.min.z = (t[0] - s[0] / 2, t[1] - s[1] / 2, t[2] - s[2] / 2)
            reply.bounds.max.x, reply.bounds.max.y, reply.bounds.max.z = (t[0] + s[0] / 2, t[1] + s[1] / 2, t[2] + s[2] / 2)
    elif which == "find_prims":
        import re
        req = cmd.find_prims
        root = req.root or "/"
        pat = re.compile(req.name_regex) if req.name_regex else None
        for path, info in state["stage"].items():
            if root != "/" and not (path == root or path.startswith(root + "/")):
                continue
            if path == root:
                continue
            if req.type_name and info.get("type") != req.type_name:
                continue
            if pat and not pat.search(path.rsplit("/", 1)[-1]):
                continue
            pi = reply.prim_list.prims.add()
            pi.path = path
            pi.type_name = info.get("type", "")
    elif which == "list_prims":
        root = cmd.list_prims.root or "/"
        for path, info in state["stage"].items():
            if root != "/" and not (path == root or path.startswith(root + "/")):
                continue
            if path == root:
                continue
            if not cmd.list_prims.recursive and (path.rsplit("/", 1)[0] or "/") != root:
                continue
            pi = reply.prim_list.prims.add()
            pi.path = path
            pi.type_name = info["type"]
    elif which == "get_prim":
        info = state["stage"].get(cmd.get_prim.prim_path)
        if info is None:
            reply.prim_desc.exists = False
        else:
            reply.prim_desc.exists = True
            reply.prim_desc.type_name = info["type"]
            reply.prim_desc.attributes.extend(info["attrs"].keys())
            reply.prim_desc.children.extend(
                x for x in state["stage"] if x.rsplit("/", 1)[0] == cmd.get_prim.prim_path)
            reply.prim_desc.active = info.get("active", True)
            reply.prim_desc.applied_apis.extend(info.get("apis", []))
    elif which == "get_attribute":
        info = state["stage"].get(cmd.get_attribute.prim_path)
        if info and cmd.get_attribute.name in info["attrs"]:
            reply.attribute.exists = True
            reply.attribute.value.CopyFrom(info["attrs"][cmd.get_attribute.name])
        else:
            reply.attribute.exists = False
    elif which == "set_attribute":
        info = state["stage"].setdefault(cmd.set_attribute.prim_path, {"type": "", "attrs": {}})
        stored = pb.UsdValue()
        stored.CopyFrom(cmd.set_attribute.value)
        info["attrs"][cmd.set_attribute.name] = stored
    elif which == "set_visibility":
        state["stage"].setdefault(cmd.set_visibility.prim_path, {"type": "", "attrs": {}, "xform": _identity_xform()})["visible"] = cmd.set_visibility.visible
    elif which == "set_active":
        state["stage"].setdefault(cmd.set_active.prim_path, {"type": "", "attrs": {}, "xform": _identity_xform()})["active"] = cmd.set_active.active
    elif which == "apply_schema":
        entry = state["stage"].setdefault(cmd.apply_schema.prim_path, {"type": "", "attrs": {}, "xform": _identity_xform()})
        entry.setdefault("apis", []).append(cmd.apply_schema.schema)
    elif which == "set_mass":
        state["stage"].setdefault(cmd.set_mass.prim_path, {"type": "", "attrs": {}, "xform": _identity_xform()})["mass"] = cmd.set_mass.mass
    elif which == "create_material":
        path = cmd.create_material.prim_path or "/World/Materials/Material"
        state["stage"][path] = {"type": "Material", "attrs": {}, "xform": _identity_xform()}
        reply.prim.prim_path = path
    elif which == "bind_material":
        pass
    elif which == "set_rigid_pose":
        req = cmd.set_rigid_pose
        xf = state["stage"].setdefault(req.prim_path, {"type": "", "attrs": {}, "xform": _identity_xform()}).setdefault("xform", _identity_xform())
        xf["t"] = [req.position.x, req.position.y, req.position.z]
        xf["o"] = [req.orientation.w, req.orientation.x, req.orientation.y, req.orientation.z]
    elif which == "set_velocity":
        req = cmd.set_velocity
        state["stage"].setdefault(req.prim_path, {"type": "", "attrs": {}, "xform": _identity_xform()})["vel"] = (
            [req.linear.x, req.linear.y, req.linear.z], [req.angular.x, req.angular.y, req.angular.z])
    elif which == "get_velocity":
        lin, ang = state["stage"].get(cmd.get_velocity.prim_path, {}).get("vel", ([0.0, 0.0, 0.0], [0.0, 0.0, 0.0]))
        reply.velocity.linear.x, reply.velocity.linear.y, reply.velocity.linear.z = lin
        reply.velocity.angular.x, reply.velocity.angular.y, reply.velocity.angular.z = ang
    elif which == "raycast":
        req = cmd.raycast
        dist = req.max_distance / 2.0
        reply.raycast.hit = True
        reply.raycast.prim_path = "/World/mockHit"
        reply.raycast.distance = dist
        reply.raycast.position.x = req.origin.x + req.direction.x * dist
        reply.raycast.position.y = req.origin.y + req.direction.y * dist
        reply.raycast.position.z = req.origin.z + req.direction.z * dist
    elif which == "move_prim":
        req = cmd.move_prim
        if req.prim_path in state["stage"]:
            state["stage"][req.new_path] = state["stage"].pop(req.prim_path)
        reply.prim.prim_path = req.new_path
    elif which == "duplicate_prim":
        req = cmd.duplicate_prim
        src = state["stage"].get(req.prim_path)
        if src is not None:
            state["stage"][req.new_path] = {
                "type": src.get("type", ""),
                "attrs": dict(src.get("attrs", {})),
                "xform": dict(src.get("xform", _identity_xform())),
            }
        reply.prim.prim_path = req.new_path
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

    state = {"frame": 0, "dofs": 9, "targets": None, "sensors": {}, "subscriptions": set(), "stage": {}}

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
