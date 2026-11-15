"""Capture the benchmark client after establishing the caller message queue; no display or colour setting changes."""
import ctypes
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


def save_client(pid: int, destination: Path):
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
    # AttachThreadInput requires a message queue on both threads. Read-only PeekMessage creates ours.
    user.PeekMessageW.argtypes = [ctypes.POINTER(wintypes.MSG), wintypes.HWND, wintypes.UINT, wintypes.UINT, wintypes.UINT]
    user.AttachThreadInput.argtypes = [wintypes.DWORD, wintypes.DWORD, wintypes.BOOL]
    user.AttachThreadInput.restype = wintypes.BOOL
    pending = wintypes.MSG()
    user.PeekMessageW(ctypes.byref(pending), None, 0, 0, 0)
    foreground = user.GetForegroundWindow()
    thread = ctypes.windll.kernel32.GetCurrentThreadId()
    foreground_thread = user.GetWindowThreadProcessId(foreground, None) if foreground else 0
    attached = bool(foreground_thread and foreground_thread != thread and user.AttachThreadInput(thread, foreground_thread, True))
    user.ShowWindow(handle, 9)
    user.BringWindowToTop(handle)
    user.SetForegroundWindow(handle)
    if attached: user.AttachThreadInput(thread, foreground_thread, False)
    time.sleep(0.2)
    if user.GetForegroundWindow() != handle:
        raise RuntimeError('Game window is occluded; refusing a desktop capture (message_queue_ready=True, attached='+str(attached)+')')
    rect = wintypes.RECT()
    origin = wintypes.POINT()
    user.GetClientRect(handle, ctypes.byref(rect))
    user.ClientToScreen(handle, ctypes.byref(origin))
    destination.parent.mkdir(parents=True, exist_ok=True)
    with ImageGrab.grab(bbox=(origin.x, origin.y, origin.x + rect.right, origin.y + rect.bottom), all_screens=True) as capture:
        capture.save(destination)
