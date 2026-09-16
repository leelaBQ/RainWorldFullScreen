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
        public const string PLUGIN_VERSION = "1.0.0";

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

            IntPtr hwnd = IntPtr.Zero;
            for (int i = 0; i < 30 && hwnd == IntPtr.Zero; i++)
            {
                using (Process process = Process.GetCurrentProcess())
                    hwnd = process.MainWindowHandle;

                if (hwnd == IntPtr.Zero)
                    yield return null;
            }

            if (hwnd == IntPtr.Zero)
            {
                Logger.LogError("Stay Visible could not obtain Rain World's window handle.");
                yield break;
            }

            MONITORINFO monitor = GetMonitor(hwnd);
            int width = monitor.rcMonitor.Right - monitor.rcMonitor.Left;
            int height = monitor.rcMonitor.Bottom - monitor.rcMonitor.Top;

            // Community-proven strategy used by BepInEx.GraphicsSettings:
            // keep Unity genuinely windowed, then remove the native Windows frame.
            if (Screen.fullScreen)
            {
                Screen.SetResolution(width, height, false);
                yield return null;
                yield return null;
            }

            // Unity may recreate/rebind the native window after changing display mode.
            using (Process process = Process.GetCurrentProcess())
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    hwnd = process.MainWindowHandle;
            }

            MakeBorderless(hwnd, monitor);
            Application.runInBackground = true;

            Logger.LogInfo($"Stay Visible applied: borderless {width}x{height}; runInBackground=true.");
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
