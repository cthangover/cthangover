"""Optional diagnostic-overlay handshake for an independent native window capture."""
import contextlib
import os
import time


@contextlib.contextmanager
def hidden_overlay(pid, enabled=True):
    if not enabled or os.name != 'nt':
        yield False
        return
    import ctypes
    from ctypes import wintypes
    path = r'\\.\pipe\cthangover-perf-overlay-' + str(pid)
    kernel = ctypes.windll.kernel32
    kernel.WaitNamedPipeW.argtypes = [wintypes.LPCWSTR, wintypes.DWORD]
    # A missing pipe means the optional mod is absent. A busy live bridge must not be silently captured.
    if not kernel.WaitNamedPipeW(path, 2000):
        error = kernel.GetLastError()
        if error in (2, 3):
            yield False
            return
        raise OSError(error, 'Diagnostic overlay capture bridge unavailable')
    with open(path, 'r+b', buffering=0) as pipe:
        pipe.write(b'\x01')
        import msvcrt
        handle = wintypes.HANDLE(msvcrt.get_osfhandle(pipe.fileno()))
        kernel.PeekNamedPipe.argtypes = [wintypes.HANDLE, wintypes.LPVOID, wintypes.DWORD, wintypes.LPVOID, ctypes.POINTER(wintypes.DWORD), wintypes.LPVOID]
        deadline = time.monotonic() + 8
        available = wintypes.DWORD()
        while True:
            if not kernel.PeekNamedPipe(handle, None, 0, None, ctypes.byref(available), None):
                raise OSError(kernel.GetLastError(), 'Diagnostic capture bridge disconnected')
            if available.value:
                break
            if time.monotonic() >= deadline:
                raise TimeoutError('Diagnostic overlay did not render a clean frame within eight seconds')
            time.sleep(.01)
        if pipe.read(1) != b'\x01':
            raise RuntimeError('Diagnostic overlay did not acknowledge a clean rendered frame')
        try:
            yield True
        finally:
            pipe.write(b'\x00')
