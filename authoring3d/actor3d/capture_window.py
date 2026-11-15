"""Capture only the running demo's client area for a colour-pipeline comparison."""
import ctypes
from ctypes import wintypes
from pathlib import Path

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


def save_client(pid: int, destination: Path):
    from PIL import ImageGrab
    user = ctypes.windll.user32
    user.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
    user.SetForegroundWindow.argtypes = [wintypes.HWND]
    user.GetClientRect.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]
    user.ClientToScreen.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.POINT)]
    _windows.clear()
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    callback = callback_type(visit)
    user.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
    user.EnumWindows(callback, pid)
    if not _windows:
        raise RuntimeError('Demo client window not found')
    handle = _windows[0]
    user.SetForegroundWindow(handle)
    rect = wintypes.RECT()
    origin = wintypes.POINT()
    user.GetClientRect(handle, ctypes.byref(rect))
    user.ClientToScreen(handle, ctypes.byref(origin))
    destination.parent.mkdir(parents=True, exist_ok=True)
    with ImageGrab.grab(bbox=(origin.x, origin.y, origin.x + rect.right, origin.y + rect.bottom), all_screens=True) as capture:
        capture.save(destination)
