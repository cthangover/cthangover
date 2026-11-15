"""Capture only the running demo's client area for a colour-pipeline comparison."""
import ctypes
import json
from ctypes import wintypes
from pathlib import Path
import time

_windows = []


def visit(handle, pid):
    user = ctypes.windll.user32
    user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user.IsWindowVisible.argtypes = [wintypes.HWND]
    process = wintypes.DWORD()
    user.GetWindowThreadProcessId(handle, ctypes.byref(process))
    if process.value == pid and user.IsWindowVisible(handle):
        _windows.append(handle)
    return True


def window_state(user, handle):
    process = wintypes.DWORD()
    name = ctypes.create_unicode_buffer(256)
    user.GetWindowThreadProcessId(handle, ctypes.byref(process))
    user.GetClassNameW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
    user.GetClassNameW(handle, name, len(name))
    user.IsWindowEnabled.argtypes = [wintypes.HWND]
    return dict(handle=handle, pid=process.value, window_class=name.value, enabled=bool(user.IsWindowEnabled(handle)))


def save_client(pid: int, destination: Path, window_handle=None, hide_diagnostics=True):
    from PIL import ImageGrab
    user = ctypes.windll.user32
    user.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
    user.SetForegroundWindow.argtypes = [wintypes.HWND]
    user.GetForegroundWindow.restype = wintypes.HWND
    user.ShowWindow.argtypes = [wintypes.HWND, ctypes.c_int]
    user.BringWindowToTop.argtypes = [wintypes.HWND]
    user.GetClientRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
    user.ClientToScreen.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.POINT)]
    _windows.clear()
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    callback = callback_type(visit)
    user.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
    user.EnumWindows(callback, pid)
    if not _windows:
        raise RuntimeError('Demo client window not found')
    # Transient tooltips/popups share the process but are not its game viewport.
    # Choose the largest visible client before requiring actual foreground ownership.
    handle = None
    largest_area = -1
    for candidate in _windows:
        client = wintypes.RECT()
        if user.GetClientRect(candidate, ctypes.byref(client)):
            area = client.right * client.bottom
            if area > largest_area:
                largest_area = area
                handle = candidate
    if handle is None or largest_area <= 0:
        raise RuntimeError('Demo client has no visible area')
    if window_handle is not None:
        handle = int(window_handle)
        owner = wintypes.DWORD()
        user.GetWindowThreadProcessId(handle, ctypes.byref(owner))
        if owner.value != pid or not user.IsWindowVisible(handle):
            raise RuntimeError('The supplied game window is not a visible window of the demo process')
    foreground = user.GetForegroundWindow()
    thread = ctypes.windll.kernel32.GetCurrentThreadId()
    foreground_thread = user.GetWindowThreadProcessId(foreground, None) if foreground else 0
    if foreground != handle:
        if foreground_thread: user.AttachThreadInput(thread, foreground_thread, True)
        user.ShowWindow(handle, 9)
        user.BringWindowToTop(handle)
        user.SetForegroundWindow(handle)
        if foreground_thread: user.AttachThreadInput(thread, foreground_thread, False)
    time.sleep(0.2)
    unlocked = False
    if user.GetForegroundWindow() != handle:
        # Windows can reject ordinary activation after an unattended child launch.
        # A balanced Alt press releases the foreground lock; no text or pointer event is sent.
        user.keybd_event(0x12, 0, 0, 0)
        user.keybd_event(0x12, 0, 2, 0)
        user.SetForegroundWindow(handle)
        unlocked = True
        time.sleep(0.2)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.with_suffix('.window.json').write_text(json.dumps(dict(selected=window_state(user, handle), foreground_before=window_state(user, foreground), foreground=window_state(user, user.GetForegroundWindow()), focus_requested=foreground != handle, focus_unlock_requested=unlocked), indent=2))
    if user.GetForegroundWindow() != handle:
        raise RuntimeError('Game window is occluded; refusing a desktop capture')
    rect = wintypes.RECT()
    origin = wintypes.POINT()
    user.GetClientRect(handle, ctypes.byref(rect))
    user.ClientToScreen(handle, ctypes.byref(origin))
    destination.parent.mkdir(parents=True, exist_ok=True)
    import sys
    bridge_folder = str(Path(__file__).resolve().parents[1] / 'perf_overlay')
    if bridge_folder not in sys.path:
        sys.path.insert(0, bridge_folder)
    from capture_overlay import hidden_overlay
    with hidden_overlay(pid, hide_diagnostics):
        with ImageGrab.grab(bbox=(origin.x, origin.y, origin.x + rect.right, origin.y + rect.bottom), all_screens=True) as capture:
            capture.save(destination)
