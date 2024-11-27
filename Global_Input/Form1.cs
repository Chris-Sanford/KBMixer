using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Global_Input
{
    public partial class Form1 : Form
    {
        // Importing necessary functions from the Windows API
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Constants for hook type and key codes
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int VK_RETURN = 0x0D;

        // Delegate for the low-level keyboard hook procedure
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Keyboard hook handle
        private IntPtr hookHandle = IntPtr.Zero;

        public Form1()
        {
            InitializeComponent();
            hookHandle = SetHook(KeyboardHookCallback); // Set the low-level keyboard hook
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == VK_RETURN)
                {
                    // Show a message box if Enter is pressed
                    MessageBox.Show("Enter key pressed!");
                }
            }

            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        protected void DisposeHookHandle(bool disposing)
        {
            if (disposing && hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookHandle); // Unhook the low-level keyboard hook
            }

            base.Dispose(disposing);
        }
    }
}
