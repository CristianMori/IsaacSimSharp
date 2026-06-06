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
        self._stage_utils.create_new_stage()

    def _h_open_stage(self, cmd, reply) -> None:
        path = os.path.abspath(cmd.open_stage.path)
        if not os.path.exists(path):
            raise FileNotFoundError(path)
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

    # ------------------------------------------------------------------ helpers
    @staticmethod
    def _isaac_version() -> str:
        try:
            import omni.kit.app

            return omni.kit.app.get_app().get_app_version()
        except Exception:  # noqa: BLE001
            return "6.0.0"
