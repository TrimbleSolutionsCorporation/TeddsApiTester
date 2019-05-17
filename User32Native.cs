using System;
using System.Runtime.InteropServices;

namespace TeddsAPITester
{
    internal static class User32Native
    {
        [DllImport(User32Dll)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private const string User32Dll = "user32.dll";
    }
}
