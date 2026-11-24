"""Control a running Cthangover process under the same OS user."""

import argparse
import errno
import io
import json
import os
import socket
import sys
import tempfile
import time
from pathlib import Path

if __package__:
    from .dev_control_commands import BUILTIN_STRATEGIES, positive_pid, raw_request
else:
    from dev_control_commands import BUILTIN_STRATEGIES, positive_pid, raw_request

LIMIT = 16 * 1024 * 1024


class PipeUnavailable(ConnectionError):
    """The process was unreachable before a command was sent."""


DISCOVERY_ENV = "CTHANGOVER_DEV_DISCOVERY"


def discovery_path(override=None):
    """Resolve the host's discovery pointer: explicit flag, then the environment, then the OS default.

    A host may publish its pointer inside the game tree (``res://``), which no OS default can guess; the
    flag exists so such a run stays reachable without exporting an environment variable.
    """
    if override:
        return Path(override)
    environment = os.environ.get(DISCOVERY_ENV)
    if environment:
        return Path(environment)
    if os.name == "nt":
        base = Path(os.environ.get("APPDATA") or (Path.home() / "AppData" / "Roaming"))
        return base / "cthangover" / "dev_host.json"
    base = Path(os.environ.get("XDG_DATA_HOME") or (Path.home() / ".local" / "share"))
    return base / "godot" / "app_userdata" / "cthangover" / "dev_host.json"


def pipe_name(pid, discovery=None):
    """Use the endpoint the host published for this instance, falling back to the legacy per-pid name.

    The kind is an open string owned by the transport (``pipe`` today); comparison is case-insensitive so
    the client keeps talking to builds that still advertise the capitalised legacy value.
    """
    legacy = f"cthangover-dev-{pid}"
    try:
        pointer = json.loads(discovery_path(discovery).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return legacy
    if pointer.get("pid") != pid:
        return legacy
    for transport in pointer.get("transports") or []:
        kind = (transport.get("kind") or "").lower()
        if kind == "pipe" and transport.get("address"):
            return transport["address"]
    return legacy


def open_windows_pipe(name):
    path = r"\\.\pipe" + "\\" + name
    deadline = time.monotonic() + 2.0
    while True:
        try:
            return open(path, "r+b", buffering=0)
        except OSError as error:
            # Windows can report EINVAL while the last instance is disconnecting.
            if error.errno not in (errno.ENOENT, errno.EBUSY, errno.EINVAL) or time.monotonic() >= deadline:
                raise PipeUnavailable(str(error)) from error
            time.sleep(0.05)


def request(pid, payload, discovery=None):
    name = pipe_name(pid, discovery)
    message = json.dumps(payload, ensure_ascii=False, allow_nan=False).encode("utf-8") + b"\n"
    if len(message) > 64 * 1024:
        raise ValueError("Command exceeds 64 KiB.")
    if os.name == "nt":
        with open_windows_pipe(name) as pipe:
            if pipe.write(message) != len(message):
                raise OSError("The command was only partially written.")
            with io.BufferedReader(pipe) as response:
                line = response.readline(LIMIT)
    else:
        with socket.socket(socket.AF_UNIX) as pipe:
            try:
                pipe.connect(str(Path(tempfile.gettempdir()) / ("CoreFxPipe_" + name)))
            except OSError as error:
                raise PipeUnavailable(str(error)) from error
            pipe.sendall(message)
            with pipe.makefile("rb") as response:
                line = response.readline(LIMIT)
    if not line.endswith(b"\n"):
        raise ValueError("Incomplete or oversized response.")
    return json.loads(line)


def build_parser(strategies=BUILTIN_STRATEGIES):
    """Construct a client with the supplied command strategies."""
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--pid", type=positive_pid, required=True, help="PID of the running game.")
    parser.add_argument(
        "--discovery",
        metavar="PATH",
        help="Discovery document to resolve the endpoint (overrides "
             f"{DISCOVERY_ENV}; defaults to the per-user profile document).",
    )
    subparsers = parser.add_subparsers(metavar="COMMAND", required=True)
    for strategy in strategies:
        strategy.register(subparsers)
    return parser


def main(argv=None, strategies=BUILTIN_STRATEGIES):
    """Parse one invocation, send it and return the command's exit status."""
    argv = sys.argv[1:] if argv is None else argv
    if argv and argv[0].lstrip("+-").isdigit():
        parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
        parser.add_argument("pid", type=positive_pid)
        parser.add_argument("json", help="JSON object, or '-' to read it from stdin.")
        parser.add_argument("--discovery", metavar="PATH", help="Discovery document to resolve the endpoint.")
        args = parser.parse_args(argv)
        make_request = raw_request
    else:
        parser = build_parser(strategies)
        args = parser.parse_args(argv)
        make_request = args.strategy.request

    try:
        payload = make_request(args)
        answer = request(args.pid, payload, args.discovery)
    except (OSError, ValueError) as error:
        parser.exit(2, f"{error}\n")
    print(json.dumps(answer, ensure_ascii=False, indent=2))
    return 0 if answer.get("ok") else 1


if __name__ == "__main__":
    sys.exit(main())
