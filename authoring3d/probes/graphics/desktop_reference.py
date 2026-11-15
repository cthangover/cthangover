"""Only the owned game client for colour verification; focus is allowed, window/display/HDR modes are never altered."""
import ctypes
from ctypes import wintypes
from PIL import ImageGrab


_windows=[]

def visit(handle, pid):
    user=ctypes.windll.user32
    owner=wintypes.DWORD();user.GetWindowThreadProcessId(handle,ctypes.byref(owner))
    if owner.value==pid and user.IsWindowVisible(handle):
        rect=wintypes.RECT();user.GetClientRect(handle,ctypes.byref(rect));_windows.append((rect.right*rect.bottom,handle))
    return True


def capture(pid, destination):
    user=ctypes.windll.user32
    user.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
    user.GetForegroundWindow.restype=wintypes.HWND
    user.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
    user.GetClientRect.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.RECT)]
    user.ClientToScreen.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.POINT)]
    user.IsWindowVisible.argtypes=[wintypes.HWND]
    _windows.clear()
    callback_type=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
    callback=callback_type(visit);user.EnumWindows.argtypes=[callback_type,wintypes.LPARAM];user.EnumWindows(callback,pid)
    if not _windows:raise RuntimeError('Owned visible game window not found')
    handle=max(_windows)[1];previous=user.GetForegroundWindow()
    user.SetForegroundWindow.argtypes=[wintypes.HWND];user.BringWindowToTop.argtypes=[wintypes.HWND]
    foreground_thread=user.GetWindowThreadProcessId(previous,None) if previous else 0
    thread=ctypes.windll.kernel32.GetCurrentThreadId()
    attached=False
    try:
        if foreground_thread:attached=bool(user.AttachThreadInput(thread,foreground_thread,True))
        user.BringWindowToTop(handle);user.SetForegroundWindow(handle)
    finally:
        if attached:user.AttachThreadInput(thread,foreground_thread,False)
    if user.GetForegroundWindow()!=handle:raise RuntimeError('Owned game is occluded; no desktop pixels captured')
    rect=wintypes.RECT();point=wintypes.POINT();user.GetClientRect(handle,ctypes.byref(rect));user.ClientToScreen(handle,ctypes.byref(point))
    with ImageGrab.grab(bbox=(point.x,point.y,point.x+rect.right,point.y+rect.bottom),all_screens=True) as image:image.save(destination)
    return {'pid':pid,'client_origin':[point.x,point.y],'client_size':[rect.right,rect.bottom],'only_owned_foreground':True,'display_modes_untouched':True}
