"""ZeroMQ ROUTER server, polled inside the Isaac Sim step loop.

Each iteration: drain all pending commands (non-blocking) and reply, then call
sim_app.update() once to keep rendering responsive and advance the timeline when
playing. This single-threaded design matches Isaac Sim's threading model and
avoids marshaling work onto a background thread.
"""

import zmq

from isaacsim_bridge.handlers import Handlers
from isaacsim_bridge.proto import isaacsim_pb2 as pb


class BridgeServer:
    # Frames to render before serving, so the slow RTX first-frame warmup
    # happens before any client connects (otherwise the first command can
    # block long enough to trip a client timeout).
    WARMUP_FRAMES = 8

    def __init__(self, sim_app, command_endpoint: str, sensor_endpoint: str) -> None:
        self.sim_app = sim_app
        self.handlers = Handlers(sim_app)

        print(f"[isaacsim-bridge] warming up ({self.WARMUP_FRAMES} frames) ...", flush=True)
        for _ in range(self.WARMUP_FRAMES):
            self.sim_app.update()

        self._ctx = zmq.Context.instance()
        self._socket = self._ctx.socket(zmq.ROUTER)
        self._socket.bind(command_endpoint)
        self._poller = zmq.Poller()
        self._poller.register(self._socket, zmq.POLLIN)

        # PUB socket for pushed sensor frames (topic = sensor handle).
        self._pub = self._ctx.socket(zmq.PUB)
        self._pub.bind(sensor_endpoint)

        print(f"[isaacsim-bridge] listening on {command_endpoint} (sensors on {sensor_endpoint})", flush=True)

    def run(self) -> None:
        try:
            while self.sim_app.is_running():
                self._drain_commands()
                if self.handlers.should_shutdown:
                    break
                self.sim_app.update()
                self._publish_sensors()
        finally:
            self._shutdown()

    def _publish_sensors(self) -> None:
        for handle in list(self.handlers.subscriptions):
            try:
                frame = self.handlers.build_frame(handle)
            except Exception as exc:  # noqa: BLE001 - never let one sensor kill the loop
                print(f"[isaacsim-bridge] sensor '{handle}' error: {exc}", flush=True)
                continue
            if frame is not None:
                self._pub.send_multipart([handle.encode("utf-8"), frame.SerializeToString()])

    def _drain_commands(self) -> None:
        events = dict(self._poller.poll(timeout=0))
        if self._socket not in events:
            return
        while True:
            try:
                identity, payload = self._socket.recv_multipart(zmq.NOBLOCK)
            except zmq.Again:
                return
            cmd = pb.Command.FromString(payload)
            reply = self.handlers.dispatch(cmd)
            self._socket.send_multipart([identity, reply.SerializeToString()])
            if self.handlers.should_shutdown:
                return

    def _shutdown(self) -> None:
        print("[isaacsim-bridge] shutting down", flush=True)
        try:
            self._socket.close(0)
            self._pub.close(0)
        finally:
            self.sim_app.close()
