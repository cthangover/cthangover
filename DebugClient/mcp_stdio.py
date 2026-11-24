"""Relay an MCP client's standard streams to a local Cthangover Debug Client mod."""

import argparse
import os
import queue
import re
import socket
import stat
import struct
import sys
import threading
import time
from pathlib import Path

if os.name == "nt":
    import _winapi
    import ctypes
    import msvcrt
    from ctypes import wintypes


PIPE_PREFIX = "cthangover-mcp-"
UNIX_PREFIX = "CoreFxPipe_"
WINDOWS_NAMESPACE = "\\\\.\\pipe\\"
CHUNK_BYTES = 64 * 1024
MAX_MESSAGE_BYTES = 16 * 1024 * 1024
PID_LIMIT = (1 << 31) - 1
WINDOWS_PIPE_NOT_CONNECTED = 233


class WindowsPipe:
    """Use independent overlapped read and write operations on one byte-mode pipe."""

    def __init__(self, pid):
        # Security QoS specifies identification only if a server impersonates its client.
        security_identification = 0x00110000
        self.handle = _winapi.CreateFile(
            WINDOWS_NAMESPACE + PIPE_PREFIX + str(pid),
            _winapi.GENERIC_READ | _winapi.GENERIC_WRITE, 0, _winapi.NULL,
            _winapi.OPEN_EXISTING, _winapi.FILE_FLAG_OVERLAPPED | security_identification,
            _winapi.NULL,
        )
        self.lock = threading.Lock()
        self.pending = set()
        self.drained = threading.Event()
        self.closed = False
        try:
            server_pid = wintypes.ULONG()
            get_server_pid = ctypes.WinDLL("kernel32", use_last_error=True).GetNamedPipeServerProcessId
            get_server_pid.argtypes = (wintypes.HANDLE, ctypes.POINTER(wintypes.ULONG))
            get_server_pid.restype = wintypes.BOOL
            if not get_server_pid(self.handle, ctypes.byref(server_pid)):
                raise ctypes.WinError(ctypes.get_last_error())
            if server_pid.value != pid:
                raise PermissionError(f"Pipe for PID {pid} belongs to process {server_pid.value}.")
        except BaseException:
            self.close()
            raise

    def transfer(self, operation, value):
        with self.lock:
            if self.closed:
                raise BrokenPipeError("The MCP pipe is closed.")
            pending, _ = operation(self.handle, value, overlapped=True)
            self.pending.add(pending)
        try:
            count, status = pending.GetOverlappedResult(True)
            if status == _winapi.ERROR_OPERATION_ABORTED:
                raise BrokenPipeError("The MCP pipe was closed.")
            if status:
                raise ctypes.WinError(status)
            return pending, count
        finally:
            with self.lock:
                self.pending.remove(pending)
                if self.closed and not self.pending:
                    self.close_handle()

    def recv(self, size):
        try:
            pending, _ = self.transfer(_winapi.ReadFile, size)
            return pending.getbuffer()
        except OSError as error:
            if closed_pipe(error):
                return b""
            raise

    def sendall(self, data):
        while data:
            _, count = self.transfer(_winapi.WriteFile, data)
            if not count:
                raise BrokenPipeError("The MCP pipe accepted no data.")
            data = data[count:]

    def close(self):
        with self.lock:
            if self.closed:
                return
            self.closed = True
            if self.pending:
                for operation in self.pending:
                    operation.cancel()
            else:
                self.close_handle()
        self.drained.wait(1)

    def close_handle(self):
        try:
            _winapi.CloseHandle(self.handle)
        finally:
            self.drained.set()


def unix_pipe_root():
    """Match the temp directory used by .NET's local named pipes on Unix."""
    return Path(os.environ.get("TMPDIR") or "/tmp")


def available_pids():
    """List listening targets by their OS endpoints, without opening a session."""
    if os.name == "nt":
        pattern = re.compile(PIPE_PREFIX + r"([1-9][0-9]{0,9})", re.IGNORECASE)
        return sorted({int(match[1]) for name in os.listdir(WINDOWS_NAMESPACE)
                       if (match := pattern.fullmatch(name)) and int(match[1]) <= PID_LIMIT})

    pattern = re.compile(UNIX_PREFIX + PIPE_PREFIX + r"([1-9][0-9]{0,9})")
    pids = set()
    with os.scandir(unix_pipe_root()) as entries:
        for entry in entries:
            match = pattern.fullmatch(entry.name)
            if not match:
                continue
            pid = int(match[1])
            if pid > PID_LIMIT:
                continue
            try:
                endpoint = entry.stat(follow_symlinks=False)
                if endpoint.st_uid == os.geteuid() and stat.S_ISSOCK(endpoint.st_mode):
                    os.kill(pid, 0)
                    pids.add(pid)
            except (FileNotFoundError, ProcessLookupError, PermissionError):
                continue
    return sorted(pids)


def connect_unix(pid, timeout):
    path = unix_pipe_root() / (UNIX_PREFIX + PIPE_PREFIX + str(pid))
    state = path.lstat()
    if not stat.S_ISSOCK(state.st_mode) or state.st_uid != os.geteuid():
        raise PermissionError(f"MCP endpoint for PID {pid} is not a socket owned by the current user.")

    endpoint = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    try:
        endpoint.settimeout(timeout)
        endpoint.connect(os.fspath(path))
        if hasattr(socket, "SO_PEERCRED"):
            peer = endpoint.getsockopt(socket.SOL_SOCKET, socket.SO_PEERCRED, struct.calcsize("iII"))
            peer_pid, peer_uid, _ = struct.unpack("iII", peer)
            if peer_pid != pid or peer_uid != os.geteuid():
                raise PermissionError(f"Unexpected process identity for MCP endpoint PID {pid}.")
        endpoint.settimeout(None)
        return endpoint
    except BaseException:
        endpoint.close()
        raise


def connect(pid, timeout):
    deadline = time.monotonic() + timeout
    while True:
        candidates = [pid] if pid is not None else available_pids()
        if len(candidates) > 1:
            ids = ", ".join(map(str, candidates))
            raise ConnectionError(f"Multiple Cthangover MCP targets are available ({ids}). Pass --pid PID.")
        remaining = deadline - time.monotonic()
        if candidates:
            target = candidates[0]
            try:
                if os.name == "nt":
                    return WindowsPipe(target)
                return connect_unix(target, max(0.01, min(0.25, remaining)))
            except (FileNotFoundError, ConnectionRefusedError, TimeoutError):
                pass
            except OSError as error:
                if os.name != "nt" or getattr(error, "winerror", None) not in (
                    _winapi.ERROR_PIPE_BUSY, WINDOWS_PIPE_NOT_CONNECTED,
                ):
                    raise
        if remaining <= 0:
            target = f" for PID {pid}" if pid is not None else ""
            raise ConnectionError(f"No reachable Cthangover MCP endpoint{target}. Start the game with debug_client enabled.")
        time.sleep(min(remaining, 0.05))


def closed_pipe(error):
    return isinstance(error, (BrokenPipeError, ConnectionResetError)) or (
        os.name == "nt" and getattr(error, "winerror", None) in (
            _winapi.ERROR_BROKEN_PIPE, _winapi.ERROR_NO_DATA, WINDOWS_PIPE_NOT_CONNECTED,
        )
    )


def write_stdout(data):
    remaining = memoryview(data)
    descriptor = sys.stdout.fileno()
    while remaining:
        written = os.write(descriptor, remaining)
        if not written:
            raise BrokenPipeError("MCP stdout closed.")
        remaining = remaining[written:]


def read_stdin(size):
    return os.read(sys.stdin.fileno(), size)


def pump(read, write, completion):
    """Keep frame size bounded without buffering or interpreting the JSON-RPC stream."""
    size = 0
    try:
        while chunk := read(CHUNK_BYTES):
            start = 0
            while (newline := chunk.find(b"\n", start)) >= 0:
                if size + newline - start >= MAX_MESSAGE_BYTES:
                    raise ValueError("MCP message exceeds 16 MiB.")
                size = 0
                start = newline + 1
            size += len(chunk) - start
            if size >= MAX_MESSAGE_BYTES:
                raise ValueError("MCP message exceeds 16 MiB.")
            write(chunk)
    except OSError as error:
        completion.put(None if closed_pipe(error) else error)
    except Exception as error:
        completion.put(error)
    else:
        completion.put(None)


def relay(endpoint):
    if os.name == "nt":
        msvcrt.setmode(sys.stdin.fileno(), os.O_BINARY)
        msvcrt.setmode(sys.stdout.fileno(), os.O_BINARY)
    completion = queue.Queue()
    # Use unbuffered stdio descriptors so an idle input thread can be left blocked
    # when the game closes the pipe. The process exits without a buffered-reader lock.
    threading.Thread(target=pump, args=(read_stdin, endpoint.sendall, completion), daemon=True).start()
    threading.Thread(target=pump, args=(endpoint.recv, write_stdout, completion), daemon=True).start()
    try:
        while True:
            try:
                error = completion.get(timeout=0.25)
                break
            except queue.Empty:
                continue
        if error is not None:
            raise error
    finally:
        if isinstance(endpoint, socket.socket):
            try:
                endpoint.shutdown(socket.SHUT_RDWR)
            except OSError:
                pass
        endpoint.close()


def positive_pid(value):
    try:
        result = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError("PID must be a positive process number.") from error
    if not 0 < result <= PID_LIMIT:
        raise argparse.ArgumentTypeError("PID must be a positive process number.")
    return result


def connect_timeout(value):
    try:
        result = float(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError("Timeout must be between 0.1 and 60 seconds.") from error
    if not 0.1 <= result <= 60:
        raise argparse.ArgumentTypeError("Timeout must be between 0.1 and 60 seconds.")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--pid", type=positive_pid, help="Select the game PID when several targets are running.")
    parser.add_argument("--connect-timeout", type=connect_timeout, default=5.0,
                        metavar="SECONDS", help="Time to wait for a local MCP endpoint (default: 5 seconds).")
    args = parser.parse_args(argv)
    try:
        relay(connect(args.pid, args.connect_timeout))
    except KeyboardInterrupt:
        return 130
    except (OSError, ValueError) as error:
        print(f"Cthangover MCP: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
