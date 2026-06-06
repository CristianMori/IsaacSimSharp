"""Command handlers that call the real Isaac Sim 6.0 API.

This module is imported AFTER SimulationApp() has been constructed, so the
isaacsim/omni modules below are available. Each command is dispatched to a
fully-formed Reply; any exception is converted into an error reply so a single
bad command never tears down the simulation loop.
"""

import os

from isaacsim_bridge import PROTOCOL_VERSION, __version__
from isaacsim_bridge.proto import isaacsim_pb2 as pb


class Handlers:
    def __init__(self, sim_app) -> None:
        self.sim_app = sim_app
        self.should_shutdown = False
        self._frame = 0
        self._articulations: dict = {}  # prim_path -> Articulation wrapper

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
        self._articulations.clear()  # old wrappers point at prims that no longer exist
        self._stage_utils.create_new_stage()

    def _h_open_stage(self, cmd, reply) -> None:
        path = os.path.abspath(cmd.open_stage.path)
        if not os.path.exists(path):
            raise FileNotFoundError(path)
        self._articulations.clear()
        self._omni_usd.get_context().open_stage(path)

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
        from isaacsim.asset.importer.urdf.impl import URDFImporter, URDFImporterConfig

        req = cmd.import_urdf
        config = URDFImporterConfig()
        config.urdf_path = req.urdf_path
        config.fix_base = req.fixed_base
        importer = URDFImporter(config)
        result = importer.import_urdf()
        reply.prim.prim_path = req.prim_path or str(result)

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

    # ------------------------------------------------------------------ helpers
    @staticmethod
    def _isaac_version() -> str:
        try:
            import omni.kit.app

            return omni.kit.app.get_app().get_app_version()
        except Exception:  # noqa: BLE001
            return "6.0.0"
