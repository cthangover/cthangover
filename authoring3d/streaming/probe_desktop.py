"""Read-only interactive-desktop status for a 3D capture investigation. No desktop/window/display changes."""
import argparse
import ctypes
from ctypes import wintypes
import datetime
import json
from pathlib import Path


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--out',type=Path,required=True);args=p.parse_args()
    user=ctypes.WinDLL('user32',use_last_error=True);kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    user.GetForegroundWindow.restype=wintypes.HWND
    user.OpenInputDesktop.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];user.OpenInputDesktop.restype=wintypes.HANDLE
    user.CloseDesktop.argtypes=[wintypes.HANDLE];user.CloseDesktop.restype=wintypes.BOOL
    user.GetUserObjectInformationW.argtypes=[wintypes.HANDLE,ctypes.c_int,wintypes.LPVOID,wintypes.DWORD,ctypes.POINTER(wintypes.DWORD)]
    row=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),foreground_present=bool(user.GetForegroundWindow()))
    ctypes.set_last_error(0);desktop=user.OpenInputDesktop(0,False,1)
    row['input_desktop_accessible']=bool(desktop);row['input_desktop_error']=ctypes.get_last_error() if not desktop else 0
    if desktop:
        try:
            size=wintypes.DWORD();user.GetUserObjectInformationW(desktop,2,None,0,ctypes.byref(size));name=ctypes.create_unicode_buffer(max(1,size.value//ctypes.sizeof(ctypes.c_wchar)))
            if user.GetUserObjectInformationW(desktop,2,name,ctypes.sizeof(name),ctypes.byref(size)):row['input_desktop_name']=name.value
        finally:user.CloseDesktop(desktop)
    session=wintypes.DWORD();kernel.ProcessIdToSessionId(kernel.GetCurrentProcessId(),ctypes.byref(session));row['watcher_session_id']=session.value
    wts=ctypes.WinDLL('wtsapi32',use_last_error=True);wts.WTSQuerySessionInformationW.argtypes=[wintypes.HANDLE,wintypes.DWORD,ctypes.c_int,ctypes.POINTER(wintypes.LPVOID),ctypes.POINTER(wintypes.DWORD)];wts.WTSFreeMemory.argtypes=[wintypes.LPVOID]
    buffer=wintypes.LPVOID();size=wintypes.DWORD()
    if wts.WTSQuerySessionInformationW(None,session.value,8,ctypes.byref(buffer),ctypes.byref(size)):
        try:row['session_connect_state']=ctypes.cast(buffer,ctypes.POINTER(ctypes.c_int)).contents.value
        finally:wts.WTSFreeMemory(buffer)
    args.out.parent.mkdir(parents=True,exist_ok=True);args.out.write_text(json.dumps(row,indent=2));print(json.dumps(row),flush=True)


if __name__=='__main__':main()
