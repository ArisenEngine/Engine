using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AvaloniaLib.Native
{
    public static class NativeAPI
    {
        // TODO: to make api platform agnostic

        // Windows API constants
        public const int WM_USER = 0x0400;
        public const int WM_CUSTOM_MESSAGE = WM_USER + 1;

        // Constants for window subclassing
        public const int GWL_WNDPROC = -4;

        public enum Win32Msg
        {
            WM_NULL             =  0x0000,
            WM_CREATE           =  0x0001,
            WM_DESTROY          =  0x0002,
            WM_MOVE             =  0x0003,
            WM_SIZE             =  0x0005,
            WM_ACTIVATE         =  0x0006,
            WM_ENTERSIZEMOVE    =  0x0231,
            WM_EXITSIZEMOVE     =  0x0232,
            WM_SIZING           =  0x0214
        }

        // Windows API function for subclassing the window
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, WindowProc newProc);

        // Define the delegate for the window procedure
        public delegate IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
