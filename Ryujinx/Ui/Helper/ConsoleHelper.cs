﻿using Ryujinx.Common.Logging;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Ui.Helper
{
    public class ConsoleHelper
    {
        public static void SetConsoleWindowState(bool show)
        {
            if (OperatingSystem.IsWindows())
            {
                SetConsoleWindowStateWindows(show);
            }
            else if (show == false)
            {
                Logger.Notice.Print(LogClass.Application, "OS doesn't support hiding console window");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void SetConsoleWindowStateWindows(bool show)
        {
            const int SW_HIDE = 0;
            const int SW_SHOW = 5;

            ShowWindow(GetConsoleWindow(), show ? SW_SHOW : SW_HIDE);
        }

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32")]
        static extern IntPtr GetConsoleWindow();

        [SupportedOSPlatform("windows")]
        [DllImport("user32")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}