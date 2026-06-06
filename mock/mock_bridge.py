"""Pure-Python mock of the Isaac Sim bridge.

Speaks the IsaacSimSharp ZeroMQ + Protobuf command protocol WITHOUT launching
Isaac Sim, so the C# SDK, tests, and samples can run on any machine with no GPU.
It returns canned replies for the handshake messages.

Run:  py -3.12 mock/mock_bridge.py
"""

import argparse
import os
import sys

import zmq

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import isaacsim_pb2 as pb  # noqa: E402  (generated from proto/isaacsim.proto)

PROTOCOL_VERSION = "0.1.0"
BRIDGE_VERSION = "mock-0.1.0"


# Lifecycle ops that simply acknowledge (no data payload) in the mock.
_ACK_ONLY = {
    "new_stage", "open_stage", "play", "pause", "stop",
    "reset", "set_physics_dt", "shutdown",
}


def handle(cmd: "pb.Command", state: dict) -> "pb.Reply":
    """Dispatch a single command to a canned reply."""
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
    elif which in _ACK_ONLY:
        pass  # ok == True, no payload
    else:
        reply.ok = False
        reply.error = f"mock bridge: unhandled request '{which}'"
    return reply


def main() -> None:
    parser = argparse.ArgumentParser(description="IsaacSimSharp mock bridge")
    parser.add_argument("--command-endpoint", default="tcp://127.0.0.1:5599")
    args = parser.parse_args()

    ctx = zmq.Context.instance()
    router = ctx.socket(zmq.ROUTER)
    router.bind(args.command_endpoint)
    print(f"[mock-bridge] listening on {args.command_endpoint}", flush=True)

    state = {"frame": 0}
    try:
        while True:
            identity, payload = router.recv_multipart()
            cmd = pb.Command.FromString(payload)
            reply = handle(cmd, state)
            router.send_multipart([identity, reply.SerializeToString()])
    except KeyboardInterrupt:
        pass
    finally:
        router.close(0)
        ctx.term()


if __name__ == "__main__":
    main()
