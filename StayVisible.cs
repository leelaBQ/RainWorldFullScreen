using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using BepInEx;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace StayVisible
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public sealed class StayVisiblePlugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "local.rainworld.stayvisible";
        public const string PLUGIN_NAME = "Stay Visible";
        public const string PLUGIN_VERSION = "1.1.0";

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;

        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        private const int WS_EX_DLGMODALFRAME = 0x00000001;
        private const int WS_EX_WINDOWEDGE = 0x00000100;
        private const int WS_EX_CLIENTEDGE = 0x00000200;
        private const int WS_EX_STATICEDGE = 0x00020000;

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private bool initialized;
        private MONITORINFO targetMonitor;

        private void Awake()
        {
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                Logger.LogWarning("Stay Visible is Windows-only; no changes were applied.");
                yield break;
            }

            // Rain World's BepInEx chainloader can start before Process.MainWindowHandle
            // becomes usable. Find the real top-level window by PID instead and wait only
            // during startup. There is no permanent polling loop.
            IntPtr hwnd = IntPtr.Zero;
            for (int frame = 0; frame < 600 && hwnd == IntPtr.Zero; frame++)
            {
                hwnd = FindLargestVisibleWindowForCurrentProcess();
                if (hwnd == IntPtr.Zero)
                    yield return null;
            }

            if (hwnd == IntPtr.Zero)
            {
                Logger.LogError("Stay Visible could not find Rain World's top-level window after 600 frames.");
                yield break;
            }

            targetMonitor = GetMonitor(hwnd);
            int width = targetMonitor.rcMonitor.Right - targetMonitor.rcMonitor.Left;
            int height = targetMonitor.rcMonitor.Bottom - targetMonitor.rcMonitor.Top;

            Logger.LogInfo($"Stay Visible found Rain World window 0x{hwnd.ToInt64():X}; target monitor={width}x{height} at ({targetMonitor.rcMonitor.Left},{targetMonitor.rcMonitor.Top}).");

            // Keep Unity genuinely windowed. Windows then sees a normal window instead
            // of a fullscreen surface that is allowed to disappear when focus changes.
            if (Screen.fullScreen)
            {
                Screen.SetResolution(width, height, false);
                yield return null;
                yield return null;
            }

            // Unity can recreate/rebind its native window after a display-mode change.
            IntPtr refreshed = FindLargestVisibleWindowForCurrentProcess();
            if (refreshed != IntPtr.Zero)
                hwnd = refreshed;

            MakeBorderless(hwnd, targetMonitor);
            Application.runInBackground = true;
            initialized = true;

            Logger.LogInfo($"Stay Visible applied: borderless {width}x{height}; runInBackground=true.");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Application.runInBackground = true;

            if (initialized)
                StartCoroutine(ReassertBorderlessNextFrame());
        }

        private IEnumerator ReassertBorderlessNextFrame()
        {
            // Event-driven only: one short reassertion when focus changes, never every frame.
            yield return null;

            int width = targetMonitor.rcMonitor.Right - targetMonitor.rcMonitor.Left;
            int height = targetMonitor.rcMonitor.Bottom - targetMonitor.rcMonitor.Top;

            if (Screen.fullScreen)
            {
                Screen.SetResolution(width, height, false);
                yield return null;
            }

            IntPtr hwnd = FindLargestVisibleWindowForCurrentProcess();
            if (hwnd != IntPtr.Zero)
                MakeBorderless(hwnd, targetMonitor);
        }

        private static IntPtr FindLargestVisibleWindowForCurrentProcess()
        {
            uint currentPid = unchecked((uint)Process.GetCurrentProcess().Id);
            IntPtr bestWindow = IntPtr.Zero;
            long bestArea = 0;

            EnumWindows((hwnd, lParam) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid != currentPid)
                    return true;

                RECT rect;
                if (!GetWindowRect(hwnd, out rect))
                    return true;

                long width = Math.Max(0, rect.Right - rect.Left);
                long height = Math.Max(0, rect.Bottom - rect.Top);
                long area = width * height;

                if (area > bestArea)
                {
                    bestArea = area;
                    bestWindow = hwnd;
                }

                return true;
            }, IntPtr.Zero);

            return bestWindow;
        }

        private static MONITORINFO GetMonitor(IntPtr hwnd)
        {
            IntPtr monitorHandle = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitorHandle == IntPtr.Zero)
                throw new InvalidOperationException("MonitorFromWindow returned null.");

            MONITORINFO info = new MONITORINFO
            {
                cbSize = Marshal.SizeOf(typeof(MONITORINFO))
            };

            if (!GetMonitorInfo(monitorHandle, ref info))
                throw new InvalidOperationException("GetMonitorInfo failed.");

            return info;
        }

        private static void MakeBorderless(IntPtr hwnd, MONITORINFO monitor)
        {
            int style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(WS_CAPTION |
                       WS_THICKFRAME |
                       WS_SYSMENU |
                       WS_MINIMIZEBOX |
                       WS_MAXIMIZEBOX);

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle &= ~(WS_EX_DLGMODALFRAME |
                         WS_EX_WINDOWEDGE |
                         WS_EX_CLIENTEDGE |
                         WS_EX_STATICEDGE);

            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

            int width = monitor.rcMonitor.Right - monitor.rcMonitor.Left;
            int height = monitor.rcMonitor.Bottom - monitor.rcMonitor.Top;

            bool ok = SetWindowPos(
                hwnd,
                IntPtr.Zero,
                monitor.rcMonitor.Left,
                monitor.rcMonitor.Top,
                width,
                height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);

            if (!ok)
                throw new InvalidOperationException("SetWindowPos failed.");
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);
    }
}
