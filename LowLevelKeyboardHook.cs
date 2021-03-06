﻿using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;

class LowLevelKeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x101;
    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;
    private List<int> blockedKeys;

    #region Singleton
    static LowLevelKeyboardHook instance = null;
    static readonly object padlock = new object();
    public static LowLevelKeyboardHook Instance
    {
        get
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    instance = new LowLevelKeyboardHook();
                }
                return instance;
            }
        }
    }
    #endregion

    public LowLevelKeyboardHook()
    {
        _hookID = SetHook(_proc);
    }

    public void SetBlockedKeys(List<int> keys)
    {
        this.blockedKeys = keys;
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);

            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                LowLevelKeyboardHook.Instance.FireKeyDownEvent(vkCode);
            }

            if (wParam == (IntPtr)WM_KEYUP)
            {
                LowLevelKeyboardHook.Instance.FireKeyUpEvent(vkCode);
            }

            if (LowLevelKeyboardHook.Instance.blockedKeys != null && LowLevelKeyboardHook.Instance.blockedKeys.Contains(vkCode))
            {
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    #region Event
    public delegate void KeyboardEventHandler(int keycode);
    public event KeyboardEventHandler KeyDownEvent;
    private void FireKeyDownEvent(int keycode)
    {
        if (KeyDownEvent != null)
        {
            KeyDownEvent(keycode);
        }
    }

    public event KeyboardEventHandler KeyUpEvent;
    private void FireKeyUpEvent(int keycode)
    {
        if (KeyUpEvent != null)
        {
            KeyUpEvent(keycode);
        }
    }
    #endregion

    #region External DLL calls
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    #endregion

    #region IDisposable Members
    public void Dispose()
    {
        UnhookWindowsHookEx(_hookID);
        KeyDownEvent = null;
    }
    #endregion
}