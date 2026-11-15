"""Read-only host timing for a native benchmark: CPU, memory and window state, without input contents."""
import ctypes
from ctypes import wintypes
import datetime
import os
import time


class LastInput(ctypes.Structure):
    _fields_=[('cbSize',wintypes.UINT),('dwTime',wintypes.DWORD)]


class GuiInfo(ctypes.Structure):
    _fields_=[('cbSize',wintypes.DWORD),('flags',wintypes.DWORD),('hwndActive',wintypes.HWND),('hwndFocus',wintypes.HWND),('hwndCapture',wintypes.HWND),('hwndMenuOwner',wintypes.HWND),('hwndMoveSize',wintypes.HWND),('hwndCaret',wintypes.HWND),('rcCaret',wintypes.RECT)]


class MemoryStatus(ctypes.Structure):
    _fields_=[('length',wintypes.DWORD),('load',wintypes.DWORD),('physical',ctypes.c_ulonglong),('available_physical',ctypes.c_ulonglong),('pagefile',ctypes.c_ulonglong),('available_pagefile',ctypes.c_ulonglong),('virtual',ctypes.c_ulonglong),('available_virtual',ctypes.c_ulonglong),('available_extended',ctypes.c_ulonglong)]


def ticks(value):
    return (value.dwHighDateTime<<32)|value.dwLowDateTime


class HostMonitor:
    def __init__(self,child):
        if os.name!='nt':raise RuntimeError('Host benchmark telemetry requires Windows')
        self.child=child
        self.kernel=ctypes.WinDLL('kernel32',use_last_error=True)
        self.user=ctypes.WinDLL('user32',use_last_error=True)
        self.kernel.GetProcessTimes.argtypes=[wintypes.HANDLE]+[ctypes.POINTER(wintypes.FILETIME)]*4
        self.kernel.GetProcessTimes.restype=wintypes.BOOL
        self.kernel.GetSystemTimes.argtypes=[ctypes.POINTER(wintypes.FILETIME)]*3
        self.kernel.GetSystemTimes.restype=wintypes.BOOL
        self.kernel.GetTickCount64.restype=ctypes.c_ulonglong
        self.kernel.GlobalMemoryStatusEx.argtypes=[ctypes.POINTER(MemoryStatus)]
        self.user.GetLastInputInfo.argtypes=[ctypes.POINTER(LastInput)]
        self.user.GetForegroundWindow.restype=wintypes.HWND
        self.user.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
        self.user.GetWindowThreadProcessId.restype=wintypes.DWORD
        self.user.GetGUIThreadInfo.argtypes=[wintypes.DWORD,ctypes.POINTER(GuiInfo)]
        self.user.GetWindowRect.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.RECT)]
        self.user.IsIconic.argtypes=[wintypes.HWND]
        self.before=None
        self.started=time.perf_counter()

    def sample(self):
        now=time.perf_counter()
        creation,exited,kernel,user=(wintypes.FILETIME() for _ in range(4))
        if not self.kernel.GetProcessTimes(int(self.child._handle),ctypes.byref(creation),ctypes.byref(exited),ctypes.byref(kernel),ctypes.byref(user)):
            raise ctypes.WinError(ctypes.get_last_error())
        process=ticks(kernel)+ticks(user)
        idle,system_kernel,system_user=(wintypes.FILETIME() for _ in range(3))
        if not self.kernel.GetSystemTimes(ctypes.byref(idle),ctypes.byref(system_kernel),ctypes.byref(system_user)):
            raise ctypes.WinError(ctypes.get_last_error())
        system=ticks(system_kernel)+ticks(system_user)
        row=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),seconds=now-self.started)
        if self.before:
            old_time,old_process,old_system,old_idle=self.before
            row['process_cpu_cores']=(process-old_process)/10000000/max(.000001,now-old_time)
            row['system_busy_fraction']=1-(ticks(idle)-old_idle)/max(1,system-old_system)
        self.before=(now,process,system,ticks(idle))
        last=LastInput(ctypes.sizeof(LastInput),0)
        if self.user.GetLastInputInfo(ctypes.byref(last)):
            row['input_idle_ms']=((self.kernel.GetTickCount64()&0xffffffff)-last.dwTime)&0xffffffff
        handle=self.user.GetForegroundWindow();pid=wintypes.DWORD()
        thread=self.user.GetWindowThreadProcessId(handle,ctypes.byref(pid)) if handle else 0
        row['game_foreground']=pid.value==self.child.pid
        if row['game_foreground']:
            info=GuiInfo();info.cbSize=ctypes.sizeof(info)
            if self.user.GetGUIThreadInfo(thread,ctypes.byref(info)):
                row['gui_move_size']=bool(info.flags&2)
                row['gui_menu']=bool(info.flags&4)
            rect=wintypes.RECT()
            if self.user.GetWindowRect(handle,ctypes.byref(rect)):row['window_rect']=[rect.left,rect.top,rect.right,rect.bottom]
            row['minimized']=bool(self.user.IsIconic(handle))
        memory=MemoryStatus();memory.length=ctypes.sizeof(memory)
        if self.kernel.GlobalMemoryStatusEx(ctypes.byref(memory)):
            row['memory_load_percent']=memory.load;row['available_physical_bytes']=memory.available_physical
        return row
