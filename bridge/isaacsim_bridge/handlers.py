"""Command handlers that call the real Isaac Sim 6.0 API.

This module is imported AFTER SimulationApp() has been constructed, so the
isaacsim/omni modules below are available. Each command is dispatched to a
fully-formed Reply; any exception is converted into an error reply so a single
bad command never tears down the simulation loop.
"""

import os

from isaacsim_bridge import PROTOCOL_VERSION, __version__
from isaacsim_bridge.proto import isaacsim_pb2 as pb


def _imu_field(data, names):
    """Best-effort extraction of a vector from the IMU get_data() result, which may be
    a dict or an object and whose exact field names vary across versions."""
    import numpy as np

    value = None
    for name in names:
        if isinstance(data, dict) and name in data:
            value = data[name]
            break
        if hasattr(data, name):
            value = getattr(data, name)
            break
    if value is None:
        return None
    try:
        return [float(x) for x in np.asarray(value).reshape(-1).tolist()]
    except Exception:  # noqa: BLE001
        return None


def _scalar_field(data, names):
    """Best-effort extraction of a scalar/bool from a dict or object."""
    import numpy as np

    value = None
    for name in names:
        if isinstance(data, dict) and name in data:
            value = data[name]
            break
        if hasattr(data, name):
            value = getattr(data, name)
            break
    if value is None:
        return None
    try:
        flat = np.asarray(value).reshape(-1)
        return flat[0] if flat.size else None
    except Exception:  # noqa: BLE001
        return value


def _usd_to_value(val):
    """Convert a USD/Gf/Vt value into a protobuf UsdValue (best-effort)."""
    from pxr import Gf, Sdf, Vt

    uv = pb.UsdValue()
    if isinstance(val, bool):  # bool before int (bool is a subclass of int)
        uv.bool_value = val
    elif isinstance(val, int):
        uv.int_value = val
    elif isinstance(val, float):
        uv.double_value = val
    elif isinstance(val, str):
        uv.string_value = val
    elif isinstance(val, Sdf.AssetPath):
        uv.string_value = val.path
    elif isinstance(val, (Gf.Vec3d, Gf.Vec3f, Gf.Vec3h, Gf.Vec3i)):
        uv.vec3_value.x, uv.vec3_value.y, uv.vec3_value.z = float(val[0]), float(val[1]), float(val[2])
    elif isinstance(val, (Gf.Vec4d, Gf.Vec4f, Gf.Vec4h, Gf.Vec4i)):
        uv.vec4_value.x, uv.vec4_value.y, uv.vec4_value.z, uv.vec4_value.w = (
            float(val[0]), float(val[1]), float(val[2]), float(val[3]))
    elif isinstance(val, (Gf.Quatd, Gf.Quatf, Gf.Quath)):
        im = val.GetImaginary()
        uv.vec4_value.x, uv.vec4_value.y, uv.vec4_value.z = float(im[0]), float(im[1]), float(im[2])
        uv.vec4_value.w = float(val.GetReal())
    elif isinstance(val, (Vt.Array, list, tuple)):
        seq = list(val)
        if all(isinstance(x, str) for x in seq):
            uv.string_array.values.extend(seq)
        elif all(isinstance(x, bool) for x in seq):
            uv.int_array.values.extend(int(x) for x in seq)
        elif all(isinstance(x, int) for x in seq):
            uv.int_array.values.extend(seq)
        else:
            try:
                uv.double_array.values.extend(float(x) for x in seq)
            except (TypeError, ValueError):
                uv.text_value = str(val)
    else:
        uv.text_value = str(val)
    return uv


def _value_to_usd(uv):
    """Convert a protobuf UsdValue into (python value, Sdf.ValueTypeName) for set/create."""
    from pxr import Gf, Sdf, Vt

    kind = uv.WhichOneof("kind")
    if kind == "bool_value":
        return bool(uv.bool_value), Sdf.ValueTypeNames.Bool
    if kind == "int_value":
        return int(uv.int_value), Sdf.ValueTypeNames.Int64
    if kind == "double_value":
        return float(uv.double_value), Sdf.ValueTypeNames.Double
    if kind == "string_value":
        return str(uv.string_value), Sdf.ValueTypeNames.String
    if kind == "token_value":
        return str(uv.token_value), Sdf.ValueTypeNames.Token
    if kind == "vec3_value":
        v = uv.vec3_value
        return Gf.Vec3d(v.x, v.y, v.z), Sdf.ValueTypeNames.Double3
    if kind == "vec4_value":
        v = uv.vec4_value
        return Gf.Vec4d(v.x, v.y, v.z, v.w), Sdf.ValueTypeNames.Double4
    if kind == "double_array":
        return Vt.DoubleArray(list(uv.double_array.values)), Sdf.ValueTypeNames.DoubleArray
    if kind == "int_array":
        return Vt.Int64Array([int(x) for x in uv.int_array.values]), Sdf.ValueTypeNames.Int64Array
    if kind == "string_array":
        return Vt.StringArray(list(uv.string_array.values)), Sdf.ValueTypeNames.StringArray
    raise ValueError(f"unsupported or empty UsdValue (kind={kind})")


class Handlers:
    def __init__(self, sim_app) -> None:
        self.sim_app = sim_app
        self.should_shutdown = False
        self._frame = 0
        self._articulations: dict = {}  # prim_path -> Articulation wrapper
        self._sensors: dict = {}        # handle -> {"type", "sensor", ...}
        self.subscriptions: set = set() # handles currently streamed over PUB/SUB

        # Heavy imports — safe now that SimulationApp exists.
        import omni.timeline
        import omni.usd
        import isaacsim.core.experimental.utils.stage as stage_utils

        self._omni_usd = omni.usd
        self._timeline = omni.timeline.get_timeline_interface()
        self._stage_utils = stage_utils

    # ------------------------------------------------------------------ dispatch
    def dispatch(self, cmd: "pb.Command") -> "pb.Reply":
        reply = pb.Reply(id=cmd.id, ok=True)
        which = cmd.WhichOneof("request")
        try:
            handler = getattr(self, f"_h_{which}", None)
            if handler is None:
                reply.ok = False
                reply.error = f"unhandled request '{which}'"
            else:
                handler(cmd, reply)
        except Exception as exc:  # noqa: BLE001 - surface any failure to the client
            reply.Clear()
            reply.id = cmd.id
            reply.ok = False
            reply.error = f"{type(exc).__name__}: {exc}"
        return reply

    # ------------------------------------------------------------------ lifecycle
    def _h_ping(self, cmd, reply) -> None:
        reply.ping.message = cmd.ping.message

    def _h_get_version(self, cmd, reply) -> None:
        v = reply.get_version
        v.isaac_sim_version = self._isaac_version()
        v.bridge_version = __version__
        v.protocol_version = PROTOCOL_VERSION

    def _h_new_stage(self, cmd, reply) -> None:
        self._reset_registries()  # old wrappers point at prims that no longer exist
        self._stage_utils.create_new_stage()

    def _h_open_stage(self, cmd, reply) -> None:
        path = os.path.abspath(cmd.open_stage.path)
        if not os.path.exists(path):
            raise FileNotFoundError(path)
        self._reset_registries()
        self._omni_usd.get_context().open_stage(path)

    def _reset_registries(self) -> None:
        self._articulations.clear()
        self._sensors.clear()
        self.subscriptions.clear()

    def _h_play(self, cmd, reply) -> None:
        self._timeline.play()

    def _h_pause(self, cmd, reply) -> None:
        self._timeline.pause()

    def _h_stop(self, cmd, reply) -> None:
        self._timeline.stop()

    def _h_reset(self, cmd, reply) -> None:
        # For an empty/simple stage this rewinds to the initial state.
        self._timeline.stop()

    def _h_step(self, cmd, reply) -> None:
        count = cmd.step.count or 1
        for _ in range(count):
            self.sim_app.update()
            self._frame += 1
        reply.step.frame = self._frame
        reply.step.sim_time = float(self._timeline.get_current_time())

    def _h_set_physics_dt(self, cmd, reply) -> None:
        from isaacsim.core.simulation_manager import SimulationManager

        SimulationManager.set_physics_dt(cmd.set_physics_dt.dt)

    def _h_export_usd(self, cmd, reply) -> None:
        path = cmd.export_usd.path
        if not path:
            raise ValueError("export path is empty")
        path = os.path.abspath(path)
        parent = os.path.dirname(path)
        if parent:
            os.makedirs(parent, exist_ok=True)
        stage = self._omni_usd.get_context().get_stage()
        if stage is None:
            raise RuntimeError("no active stage to export")
        stage.Export(path)
        reply.export_usd.path = path

    def _h_shutdown(self, cmd, reply) -> None:
        self.should_shutdown = True

    # ------------------------------------------------------------------ scene
    def _h_add_ground_plane(self, cmd, reply) -> None:
        from isaacsim.core.experimental.objects import GroundPlane

        path = cmd.add_ground_plane.prim_path or "/World/GroundPlane"
        GroundPlane(path, positions=[[0.0, 0.0, 0.0]])
        reply.prim.prim_path = path

    def _h_add_light(self, cmd, reply) -> None:
        from isaacsim.core.experimental.objects import (
            DistantLight,
            DomeLight,
            RectLight,
            SphereLight,
        )

        req = cmd.add_light
        path = req.prim_path or "/World/Light"
        cls = {
            pb.LIGHT_DISTANT: DistantLight,
            pb.LIGHT_SPHERE: SphereLight,
            pb.LIGHT_DOME: DomeLight,
            pb.LIGHT_RECT: RectLight,
        }.get(req.type, DistantLight)
        light = cls(path)
        light.set_intensities(req.intensity or 1000.0)
        if any((req.color.r, req.color.g, req.color.b)):
            try:
                light.set_colors([[req.color.r, req.color.g, req.color.b]])
            except Exception:  # noqa: BLE001 - color is best-effort across light types
                pass
        reply.prim.prim_path = path

    def _h_add_primitive(self, cmd, reply) -> None:
        import numpy as np
        from isaacsim.core.experimental.objects import (
            Capsule,
            Cone,
            Cube,
            Cylinder,
            Sphere,
        )

        req = cmd.add_primitive
        path = req.prim_path or "/World/Prim"
        pos = [[req.position.x, req.position.y, req.position.z]]
        size = req.size or 0.5

        if req.shape == pb.SHAPE_SPHERE:
            obj = Sphere(paths=path, positions=pos, radii=size)
        elif req.shape == pb.SHAPE_CYLINDER:
            obj = Cylinder(paths=path, positions=pos, radii=size, heights=size * 2.0)
        elif req.shape == pb.SHAPE_CONE:
            obj = Cone(paths=path, positions=pos, radii=size, heights=size * 2.0)
        elif req.shape == pb.SHAPE_CAPSULE:
            obj = Capsule(paths=path, positions=pos, radii=size, heights=size * 2.0)
        else:
            obj = Cube(paths=path, positions=pos, sizes=size)

        q = req.orientation
        if any((q.x, q.y, q.z, q.w)):
            obj.set_world_poses(
                positions=np.array(pos, dtype=np.float32),
                orientations=np.array([[q.w, q.x, q.y, q.z]], dtype=np.float32),
            )

        if req.collision or req.rigid:
            from isaacsim.core.experimental.prims import GeomPrim, RigidPrim

            GeomPrim(paths=path, apply_collision_apis=True)
            if req.rigid:
                RigidPrim(paths=path)

        reply.prim.prim_path = path

    def _h_add_reference(self, cmd, reply) -> None:
        req = cmd.add_reference
        if not req.usd_path:
            raise ValueError("usd_path is required")
        path = req.prim_path or "/World/Reference"
        self._stage_utils.add_reference_to_stage(usd_path=req.usd_path, path=path)
        reply.prim.prim_path = path

    def _h_set_prim_pose(self, cmd, reply) -> None:
        import numpy as np
        from isaacsim.core.experimental.prims import XformPrim

        req = cmd.set_prim_pose
        xform = XformPrim(paths=req.prim_path)
        q = req.orientation
        orient = (
            [q.w, q.x, q.y, q.z] if any((q.x, q.y, q.z, q.w)) else [1.0, 0.0, 0.0, 0.0]
        )
        xform.set_world_poses(
            positions=np.array([[req.position.x, req.position.y, req.position.z]], dtype=np.float32),
            orientations=np.array([orient], dtype=np.float32),
        )

    def _h_remove_prim(self, cmd, reply) -> None:
        stage = self._omni_usd.get_context().get_stage()
        if not stage.RemovePrim(cmd.remove_prim.prim_path):
            raise RuntimeError(f"failed to remove prim '{cmd.remove_prim.prim_path}'")

    def _h_import_urdf(self, cmd, reply) -> None:
        import tempfile

        from isaacsim.asset.importer.urdf.impl import URDFImporter, URDFImporterConfig

        self._ensure_urdf_extensions()

        req = cmd.import_urdf
        urdf_path = os.path.abspath(req.urdf_path)
        if not os.path.exists(urdf_path):
            raise FileNotFoundError(urdf_path)

        out_dir = os.path.join(tempfile.gettempdir(), "isaacsim_bridge_urdf")
        os.makedirs(out_dir, exist_ok=True)

        config = URDFImporterConfig()
        config.urdf_path = urdf_path
        config.usd_path = out_dir
        config.fix_base = req.fixed_base
        output_usd = URDFImporter(config).import_urdf()
        if not output_usd:
            raise RuntimeError(f"URDF import produced no output for {urdf_path}")

        # Reference the converted USD onto the live stage so it is usable immediately.
        name = os.path.splitext(os.path.basename(urdf_path))[0]
        prim_path = req.prim_path or f"/World/{name}"
        self._stage_utils.add_reference_to_stage(usd_path=output_usd, path=prim_path)
        reply.prim.prim_path = prim_path

    def _ensure_urdf_extensions(self) -> None:
        if getattr(self, "_urdf_ext_ready", False):
            return
        import omni.kit.app

        manager = omni.kit.app.get_app().get_extension_manager()
        for ext in ("omni.scene.optimizer.core", "isaacsim.robot.schema"):
            manager.set_extension_enabled_immediate(ext, True)
        self._urdf_ext_ready = True

    # ------------------------------------------------------------------ robots
    def _h_get_assets_root(self, cmd, reply) -> None:
        from isaacsim.storage.native import get_assets_root_path

        reply.get_assets_root.path = get_assets_root_path() or ""

    def _h_register_articulation(self, cmd, reply) -> None:
        from isaacsim.core.experimental.prims import Articulation

        path = cmd.register_articulation.prim_path
        art = Articulation(path)  # always fresh; replaces any stale wrapper
        self._articulations[path] = art
        reply.articulation.prim_path = path
        try:
            names = [str(n) for n in art.dof_names]
        except Exception:  # noqa: BLE001 - dof_names may be unavailable before play
            names = []
        reply.articulation.dof_names.extend(names)
        try:
            reply.articulation.dof_count = int(art.get_dof_positions().numpy().reshape(-1).shape[0])
        except Exception:  # noqa: BLE001
            reply.articulation.dof_count = len(names)

    def _h_get_dof_state(self, cmd, reply) -> None:
        art = self._articulation(cmd.get_dof_state.prim_path)
        reply.dof_state.positions.extend(art.get_dof_positions().numpy().reshape(-1).tolist())
        reply.dof_state.velocities.extend(art.get_dof_velocities().numpy().reshape(-1).tolist())
        try:
            reply.dof_state.efforts.extend(art.get_dof_efforts().numpy().reshape(-1).tolist())
        except Exception:  # noqa: BLE001 - efforts not always available
            pass

    def _h_set_dof_targets(self, cmd, reply) -> None:
        import numpy as np

        req = cmd.set_dof_targets
        art = self._articulation(req.prim_path)
        values = np.array([list(req.values)], dtype=np.float32)
        indices = list(req.indices) if len(req.indices) else None
        if req.mode == pb.DOF_VELOCITY:
            art.set_dof_velocity_targets(values, dof_indices=indices)
        elif req.mode == pb.DOF_EFFORT:
            art.set_dof_efforts(values, dof_indices=indices)
        else:
            art.set_dof_position_targets(values, dof_indices=indices)

    def _articulation(self, path: str):
        art = self._articulations.get(path)
        if art is None:
            from isaacsim.core.experimental.prims import Articulation

            art = Articulation(path)
            self._articulations[path] = art
        return art

    # ------------------------------------------------------------------ sensors
    def _h_create_camera(self, cmd, reply) -> None:
        import numpy as np
        from isaacsim.sensors.experimental.rtx import CameraSensor, RtxCamera

        req = cmd.create_camera
        path = req.prim_path or "/World/camera"
        width = req.width or 640
        height = req.height or 480

        kwargs = {"tick_rate": 60.0}
        if any((req.position.x, req.position.y, req.position.z)):
            kwargs["translations"] = np.array([req.position.x, req.position.y, req.position.z])
        q = req.orientation
        if any((q.x, q.y, q.z, q.w)):
            kwargs["orientations"] = np.array([q.w, q.x, q.y, q.z])

        cam = RtxCamera(path, **kwargs)
        annotators = ["rgb"] + (["distance_to_image_plane"] if req.depth else [])
        sensor = CameraSensor(cam, resolution=(height, width), annotators=annotators)
        self._sensors[path] = {"type": pb.SENSOR_CAMERA, "sensor": sensor, "depth": req.depth}
        reply.sensor.handle = path

    def _h_create_imu(self, cmd, reply) -> None:
        import numpy as np
        from isaacsim.sensors.experimental.physics import IMU, IMUSensor

        req = cmd.create_imu
        path = req.prim_path or "/World/imu_sensor"
        sensor = IMUSensor(
            IMU.create(path, translations=np.array([[req.position.x, req.position.y, req.position.z]]))
        )
        self._sensors[path] = {"type": pb.SENSOR_IMU, "sensor": sensor}
        reply.sensor.handle = path

    def _h_create_contact(self, cmd, reply) -> None:
        import numpy as np
        from isaacsim.sensors.experimental.physics import Contact, ContactSensor

        req = cmd.create_contact
        path = req.prim_path or "/World/contact_sensor"
        contact = Contact.create(
            path,
            min_threshold=req.min_threshold or 0.0,
            max_threshold=req.max_threshold or 1e7,
            radius=req.radius or 0.1,
            translations=np.array([[req.position.x, req.position.y, req.position.z]]),
        )
        sensor = ContactSensor(contact)
        try:
            sensor.add_raw_contact_data_to_frame()
        except Exception:  # noqa: BLE001
            pass
        self._sensors[path] = {"type": pb.SENSOR_CONTACT, "sensor": sensor}
        reply.sensor.handle = path

    def _h_create_lidar(self, cmd, reply) -> None:
        import numpy as np
        from isaacsim.core.experimental.utils.app import enable_extension
        from isaacsim.sensors.experimental.rtx import Lidar, LidarSensor

        enable_extension("isaacsim.sensors.rtx.nodes")
        req = cmd.create_lidar
        path = req.prim_path or "/World/lidar"
        lidar = Lidar.create(
            path,
            config=req.config or "Example_Rotary",
            translations=np.array([req.position.x, req.position.y, req.position.z]),
        )
        sensor = LidarSensor(lidar, annotators=["generic-model-output"])
        self._sensors[path] = {"type": pb.SENSOR_LIDAR, "sensor": sensor}
        reply.sensor.handle = path

    def _h_create_radar(self, cmd, reply) -> None:
        import carb
        import numpy as np
        from isaacsim.core.experimental.utils.app import enable_extension
        from isaacsim.sensors.experimental.rtx import Radar, RadarSensor

        # RTX radar needs Motion BVH (Doppler); without it the native plugin crashes the
        # process, so fail fast with a clear message instead.
        if not carb.settings.get_settings().get("/renderer/raytracingMotion/enabled"):
            raise RuntimeError("RTX radar requires Motion BVH; launch the bridge with --motion-bvh")

        enable_extension("isaacsim.sensors.rtx.nodes")
        req = cmd.create_radar
        path = req.prim_path or "/World/radar"
        radar = Radar.create(
            path,
            config=req.config or "IWRL6432AOP",
            translations=np.array([req.position.x, req.position.y, req.position.z]),
        )
        sensor = RadarSensor(radar, annotators=["generic-model-output"])
        self._sensors[path] = {"type": pb.SENSOR_RADAR, "sensor": sensor}
        reply.sensor.handle = path

    def _h_subscribe(self, cmd, reply) -> None:
        handle = cmd.subscribe.handle
        if handle not in self._sensors:
            raise KeyError(f"unknown sensor '{handle}'")
        self.subscriptions.add(handle)

    def _h_unsubscribe(self, cmd, reply) -> None:
        self.subscriptions.discard(cmd.unsubscribe.handle)

    def _h_get_sensor_frame(self, cmd, reply) -> None:
        frame = self.build_frame(cmd.get_sensor_frame.handle)
        if frame is None:
            raise RuntimeError(f"no data available yet for sensor '{cmd.get_sensor_frame.handle}'")
        reply.sensor_frame.CopyFrom(frame)

    def build_frame(self, handle: str):
        """Build a SensorFrame for a handle, or None if no data is ready yet."""
        info = self._sensors.get(handle)
        if info is None:
            raise KeyError(f"unknown sensor '{handle}'")
        if info["type"] == pb.SENSOR_CAMERA:
            return self._frame_camera(handle, info)
        if info["type"] == pb.SENSOR_IMU:
            return self._frame_imu(handle, info)
        if info["type"] == pb.SENSOR_CONTACT:
            return self._frame_contact(handle, info)
        if info["type"] == pb.SENSOR_LIDAR:
            return self._frame_gmo_point_cloud(handle, info, pb.SENSOR_LIDAR)
        if info["type"] == pb.SENSOR_RADAR:
            return self._frame_gmo_point_cloud(handle, info, pb.SENSOR_RADAR)
        return None

    def _frame_contact(self, handle, info):
        # Contact get_data() is a dict: time, physics_step, contacts, in_contact,
        # force, number_of_contacts.
        data = info["sensor"].get_data()
        frame = self._frame_header(handle, pb.SENSOR_CONTACT)
        in_contact = _scalar_field(data, ("in_contact",))
        if in_contact is not None:
            frame.contact.in_contact = bool(in_contact)
        count = _scalar_field(data, ("number_of_contacts",))
        if count is not None:
            frame.contact.count = int(count)
        force = _imu_field(data, ("force",))
        if force is not None:
            if len(force) >= 3:
                frame.contact.force.x, frame.contact.force.y, frame.contact.force.z = force[:3]
                frame.contact.force_magnitude = float((sum(c * c for c in force[:3])) ** 0.5)
            elif len(force) == 1:
                frame.contact.force_magnitude = float(force[0])
        return frame

    def _frame_gmo_point_cloud(self, handle, info, sensor_type):
        """Shared decode for RTX lidar/radar 'generic-model-output' buffers into a point cloud."""
        import numpy as np
        from isaacsim.sensors.experimental.rtx import parse_generic_model_output_data

        data, _ = info["sensor"].get_data("generic-model-output")
        if data is None:
            return None
        gmo = parse_generic_model_output_data(data)
        if gmo.numElements == 0:
            return None  # scan buffer not populated yet this frame

        # GMO spherical returns: x = azimuth(deg), y = elevation(deg), z = range(m).
        azimuth = np.radians(np.asarray(gmo.x, dtype=np.float64))
        elevation = np.radians(np.asarray(gmo.y, dtype=np.float64))
        rng = np.asarray(gmo.z, dtype=np.float64)
        cos_el = np.cos(elevation)
        pts = np.stack(
            [rng * cos_el * np.cos(azimuth), rng * cos_el * np.sin(azimuth), rng * np.sin(elevation)],
            axis=-1,
        ).astype(np.float32)

        frame = self._frame_header(handle, sensor_type)
        frame.point_cloud.count = int(pts.shape[0])
        frame.point_cloud.points = np.ascontiguousarray(pts).tobytes()
        try:
            intensity = np.asarray(gmo.scalar, dtype=np.float32).reshape(-1)
            if intensity.shape[0] == pts.shape[0]:
                frame.point_cloud.intensities = np.ascontiguousarray(intensity).tobytes()
        except Exception:  # noqa: BLE001 - intensity is optional
            pass
        return frame

    def _frame_header(self, handle, sensor_type):
        return pb.SensorFrame(
            handle=handle,
            type=sensor_type,
            frame=self._frame,
            sim_time=float(self._timeline.get_current_time()),
        )

    def _frame_camera(self, handle, info):
        import numpy as np

        sensor = info["sensor"]
        rgb, _ = sensor.get_data("rgb")
        if rgb is None:
            return None
        arr = np.ascontiguousarray(rgb.numpy())
        height, width = int(arr.shape[0]), int(arr.shape[1])
        channels = int(arr.shape[2]) if arr.ndim == 3 else 1
        frame = self._frame_header(handle, pb.SENSOR_CAMERA)
        img = frame.image
        img.width, img.height, img.channels = width, height, channels
        img.encoding = {4: "rgba8", 3: "rgb8"}.get(channels, "gray8")
        img.data = arr.astype(np.uint8).tobytes()
        if info.get("depth"):
            depth, _ = sensor.get_data("distance_to_image_plane")
            if depth is not None:
                img.depth = np.ascontiguousarray(depth.numpy()).astype(np.float32).tobytes()
        return frame

    def _frame_imu(self, handle, info):
        import numpy as np

        data = info["sensor"].get_data()
        frame = self._frame_header(handle, pb.SENSOR_IMU)
        acc = _imu_field(data, ("lin_acc", "linear_acceleration", "acceleration", "accel"))
        gyro = _imu_field(data, ("ang_vel", "angular_velocity", "gyro"))
        orient = _imu_field(data, ("orientation", "quat", "orientations"))
        if acc is not None:
            frame.imu.linear_acceleration.x, frame.imu.linear_acceleration.y, frame.imu.linear_acceleration.z = acc
        if gyro is not None:
            frame.imu.angular_velocity.x, frame.imu.angular_velocity.y, frame.imu.angular_velocity.z = gyro
        if orient is not None and len(orient) == 4:
            frame.imu.orientation.w, frame.imu.orientation.x, frame.imu.orientation.y, frame.imu.orientation.z = orient
        return frame

    # ------------------------------------------------------------------ generic USD
    def _h_list_prims(self, cmd, reply) -> None:
        from pxr import Usd

        stage = self._stage()
        root = cmd.list_prims.root or "/"
        if root == "/":
            root_prim = stage.GetPseudoRoot()
        else:
            root_prim = stage.GetPrimAtPath(root)
            if not root_prim.IsValid():
                raise KeyError(f"prim not found '{root}'")

        if cmd.list_prims.recursive:
            for prim in Usd.PrimRange(root_prim):
                if prim == root_prim:
                    continue
                info = reply.prim_list.prims.add()
                info.path = str(prim.GetPath())
                info.type_name = prim.GetTypeName()
        else:
            for child in root_prim.GetChildren():
                info = reply.prim_list.prims.add()
                info.path = str(child.GetPath())
                info.type_name = child.GetTypeName()

    def _h_define_prim(self, cmd, reply) -> None:
        stage = self._stage()
        req = cmd.define_prim
        prim = stage.DefinePrim(req.prim_path, req.type_name) if req.type_name else stage.DefinePrim(req.prim_path)
        if not prim.IsValid():
            raise RuntimeError(f"failed to define prim '{req.prim_path}'")
        reply.prim.prim_path = str(prim.GetPath())

    def _h_get_prim(self, cmd, reply) -> None:
        from pxr import Usd, UsdGeom

        prim = self._stage().GetPrimAtPath(cmd.get_prim.prim_path)
        if not prim.IsValid():
            reply.prim_desc.exists = False
            return
        desc = reply.prim_desc
        desc.exists = True
        desc.type_name = prim.GetTypeName()
        desc.attributes.extend(a.GetName() for a in prim.GetAttributes())
        desc.children.extend(str(c.GetPath()) for c in prim.GetChildren())
        desc.active = prim.IsActive()
        desc.applied_apis.extend(prim.GetAppliedSchemas())
        desc.relationships.extend(r.GetName() for r in prim.GetRelationships())
        desc.kind = Usd.ModelAPI(prim).GetKind() or ""
        imageable = UsdGeom.Imageable(prim)
        if imageable:
            desc.visibility = str(imageable.ComputeVisibility())

    def _h_get_attribute(self, cmd, reply) -> None:
        req = cmd.get_attribute
        prim = self._stage().GetPrimAtPath(req.prim_path)
        attr = prim.GetAttribute(req.name) if prim.IsValid() else None
        if not attr or not attr.IsValid() or not attr.HasValue():
            reply.attribute.exists = False
            return
        reply.attribute.exists = True
        reply.attribute.type_name = str(attr.GetTypeName())
        reply.attribute.value.CopyFrom(_usd_to_value(attr.Get()))

    def _h_set_attribute(self, cmd, reply) -> None:
        req = cmd.set_attribute
        prim = self._stage().GetPrimAtPath(req.prim_path)
        if not prim.IsValid():
            raise KeyError(f"prim not found '{req.prim_path}'")
        value, sdf_type = _value_to_usd(req.value)
        attr = prim.GetAttribute(req.name)
        if not attr or not attr.IsValid():
            attr = prim.CreateAttribute(req.name, sdf_type)
        if not attr.Set(value):
            raise RuntimeError(f"failed to set attribute '{req.name}' on '{req.prim_path}'")

    def _h_get_transform(self, cmd, reply) -> None:
        import numpy as np

        xform = self._xform(cmd.get_transform.prim_path)
        if cmd.get_transform.world:
            positions, orientations = xform.get_world_poses()
        else:
            positions, orientations = xform.get_local_poses()
        scales = xform.get_local_scales()
        p = np.asarray(positions.numpy()).reshape(-1)
        o = np.asarray(orientations.numpy()).reshape(-1)  # wxyz
        s = np.asarray(scales.numpy()).reshape(-1)
        t = reply.transform.transform
        t.translation.x, t.translation.y, t.translation.z = float(p[0]), float(p[1]), float(p[2])
        t.orientation.w, t.orientation.x, t.orientation.y, t.orientation.z = (
            float(o[0]), float(o[1]), float(o[2]), float(o[3]))
        t.scale.x, t.scale.y, t.scale.z = float(s[0]), float(s[1]), float(s[2])

    def _h_set_transform(self, cmd, reply) -> None:
        import numpy as np

        req = cmd.set_transform
        xform = self._xform(req.prim_path)
        has_t, has_o, has_s = req.HasField("translation"), req.HasField("orientation"), req.HasField("scale")
        if has_t or has_o:
            positions = np.array([[req.translation.x, req.translation.y, req.translation.z]]) if has_t else None
            orientations = (
                np.array([[req.orientation.w, req.orientation.x, req.orientation.y, req.orientation.z]])
                if has_o else None)
            if req.world:
                xform.set_world_poses(positions=positions, orientations=orientations)
            else:
                xform.set_local_poses(positions=positions, orientations=orientations)
        if has_s:
            xform.set_local_scales(np.array([[req.scale.x, req.scale.y, req.scale.z]]))

    def _h_get_bounds(self, cmd, reply) -> None:
        from pxr import Usd, UsdGeom

        prim = self._stage().GetPrimAtPath(cmd.get_bounds.prim_path)
        if not prim.IsValid():
            reply.bounds.valid = False
            return
        cache = UsdGeom.BBoxCache(Usd.TimeCode.Default(), [UsdGeom.Tokens.default_, UsdGeom.Tokens.render])
        rng = cache.ComputeWorldBound(prim).ComputeAlignedRange()
        if rng.IsEmpty():
            reply.bounds.valid = False
            return
        mn, mx = rng.GetMin(), rng.GetMax()
        reply.bounds.valid = True
        reply.bounds.min.x, reply.bounds.min.y, reply.bounds.min.z = mn[0], mn[1], mn[2]
        reply.bounds.max.x, reply.bounds.max.y, reply.bounds.max.z = mx[0], mx[1], mx[2]

    def _h_find_prims(self, cmd, reply) -> None:
        import re

        from pxr import Usd

        stage = self._stage()
        req = cmd.find_prims
        root = req.root or "/"
        root_prim = stage.GetPseudoRoot() if root == "/" else stage.GetPrimAtPath(root)
        if not root_prim.IsValid():
            raise KeyError(f"prim not found '{root}'")
        pattern = re.compile(req.name_regex) if req.name_regex else None
        for prim in Usd.PrimRange(root_prim):
            if prim == root_prim:
                continue
            if req.type_name and prim.GetTypeName() != req.type_name:
                continue
            if pattern and not pattern.search(prim.GetName()):
                continue
            if req.has_api and req.has_api not in prim.GetAppliedSchemas():
                continue
            info = reply.prim_list.prims.add()
            info.path = str(prim.GetPath())
            info.type_name = prim.GetTypeName()

    # ------------------------------------------------------------------ manipulation
    def _h_set_visibility(self, cmd, reply) -> None:
        import numpy as np

        self._xform(cmd.set_visibility.prim_path).set_visibilities(np.array([cmd.set_visibility.visible]))

    def _h_set_active(self, cmd, reply) -> None:
        prim = self._stage().GetPrimAtPath(cmd.set_active.prim_path)
        if not prim.IsValid():
            raise KeyError(f"prim not found '{cmd.set_active.prim_path}'")
        prim.SetActive(cmd.set_active.active)

    def _h_apply_schema(self, cmd, reply) -> None:
        from pxr import UsdPhysics

        prim = self._stage().GetPrimAtPath(cmd.apply_schema.prim_path)
        if not prim.IsValid():
            raise KeyError(f"prim not found '{cmd.apply_schema.prim_path}'")
        schema = cmd.apply_schema.schema
        if schema in ("PhysicsRigidBodyAPI", "RigidBodyAPI"):
            UsdPhysics.RigidBodyAPI.Apply(prim)
        elif schema in ("PhysicsCollisionAPI", "CollisionAPI"):
            UsdPhysics.CollisionAPI.Apply(prim)
        elif schema in ("PhysicsMassAPI", "MassAPI"):
            UsdPhysics.MassAPI.Apply(prim)
        else:
            prim.AddAppliedSchema(schema)

    def _h_set_mass(self, cmd, reply) -> None:
        from pxr import UsdPhysics

        prim = self._stage().GetPrimAtPath(cmd.set_mass.prim_path)
        if not prim.IsValid():
            raise KeyError(f"prim not found '{cmd.set_mass.prim_path}'")
        UsdPhysics.MassAPI.Apply(prim).CreateMassAttr().Set(float(cmd.set_mass.mass))

    def _h_create_material(self, cmd, reply) -> None:
        from pxr import Gf, Sdf, UsdShade

        req = cmd.create_material
        path = req.prim_path or "/World/Materials/Material"
        stage = self._stage()
        material = UsdShade.Material.Define(stage, path)
        shader = UsdShade.Shader.Define(stage, path + "/Shader")
        shader.CreateIdAttr("UsdPreviewSurface")
        shader.CreateInput("diffuseColor", Sdf.ValueTypeNames.Color3f).Set(
            Gf.Vec3f(req.color.r, req.color.g, req.color.b))
        shader.CreateInput("metallic", Sdf.ValueTypeNames.Float).Set(float(req.metallic))
        shader.CreateInput("roughness", Sdf.ValueTypeNames.Float).Set(float(req.roughness))
        material.CreateSurfaceOutput().ConnectToSource(shader.ConnectableAPI(), "surface")
        reply.prim.prim_path = path

    def _h_bind_material(self, cmd, reply) -> None:
        from pxr import UsdShade

        stage = self._stage()
        prim = stage.GetPrimAtPath(cmd.bind_material.prim_path)
        material = UsdShade.Material(stage.GetPrimAtPath(cmd.bind_material.material_path))
        if not prim.IsValid() or not material:
            raise KeyError("prim or material not found")
        UsdShade.MaterialBindingAPI.Apply(prim).Bind(material)

    # ------------------------------------------------------------------ runtime physics
    def _h_set_rigid_pose(self, cmd, reply) -> None:
        import numpy as np

        req = cmd.set_rigid_pose
        self._rigid(req.prim_path).set_world_poses(
            positions=np.array([[req.position.x, req.position.y, req.position.z]]),
            orientations=np.array([[req.orientation.w, req.orientation.x, req.orientation.y, req.orientation.z]]),
        )

    def _h_set_velocity(self, cmd, reply) -> None:
        import numpy as np

        req = cmd.set_velocity
        self._rigid(req.prim_path).set_velocities(
            linear_velocities=np.array([[req.linear.x, req.linear.y, req.linear.z]]),
            angular_velocities=np.array([[req.angular.x, req.angular.y, req.angular.z]]),
        )

    def _h_get_velocity(self, cmd, reply) -> None:
        import numpy as np

        linear, angular = self._rigid(cmd.get_velocity.prim_path).get_velocities()
        lin = np.asarray(linear.numpy()).reshape(-1)
        ang = np.asarray(angular.numpy()).reshape(-1)
        reply.velocity.linear.x, reply.velocity.linear.y, reply.velocity.linear.z = (
            float(lin[0]), float(lin[1]), float(lin[2]))
        reply.velocity.angular.x, reply.velocity.angular.y, reply.velocity.angular.z = (
            float(ang[0]), float(ang[1]), float(ang[2]))

    def _h_raycast(self, cmd, reply) -> None:
        from omni.physx import get_physx_scene_query_interface

        req = cmd.raycast
        origin = [req.origin.x, req.origin.y, req.origin.z]
        direction = [req.direction.x, req.direction.y, req.direction.z]
        hit = get_physx_scene_query_interface().raycast_closest(origin, direction, req.max_distance)
        if not hit or not hit.get("hit"):
            reply.raycast.hit = False
            return
        reply.raycast.hit = True
        reply.raycast.prim_path = hit.get("collision", hit.get("rigidBody", ""))
        pos = hit.get("position", [0.0, 0.0, 0.0])
        nrm = hit.get("normal", [0.0, 0.0, 0.0])
        reply.raycast.position.x, reply.raycast.position.y, reply.raycast.position.z = (
            float(pos[0]), float(pos[1]), float(pos[2]))
        reply.raycast.normal.x, reply.raycast.normal.y, reply.raycast.normal.z = (
            float(nrm[0]), float(nrm[1]), float(nrm[2]))
        reply.raycast.distance = float(hit.get("distance", 0.0))

    def _xform(self, path: str):
        from isaacsim.core.experimental.prims import XformPrim

        return XformPrim(paths=path)

    def _rigid(self, path: str):
        from isaacsim.core.experimental.prims import RigidPrim

        return RigidPrim(paths=path)

    def _stage(self):
        stage = self._omni_usd.get_context().get_stage()
        if stage is None:
            raise RuntimeError("no active stage")
        return stage

    # ------------------------------------------------------------------ helpers
    @staticmethod
    def _isaac_version() -> str:
        try:
            import omni.kit.app

            return omni.kit.app.get_app().get_app_version()
        except Exception:  # noqa: BLE001
            return "6.0.0"
