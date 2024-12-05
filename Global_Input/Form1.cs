using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Global_Input
{
    public partial class Form1 : Form
    {
        // DllImport: specifies that the method is implemented in an unmanaged DLL
        // user32.dll: the Windows API library
        // Importing necessary functions from the Windows API
        // private: only accessible within the class
        // static: shared across all instances of the class
        // extern: implemented elsewhere
        // IntPtr: a pointer to a memory location (int pointer)
        // SetWindowsHookEx: sets an application-defined hook procedure for a hook
        // int idHook: the type of hook procedure to be installed
        // LowLevelKeyboardProc callback: a pointer to the hook procedure
        // IntPtr hMod: a handle to the DLL containing the hook procedure
        // uint dwThreadId: the identifier of the thread with which the hook procedure is to be associated
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);

        // UnhookWindowsHookEx: removes a hook procedure installed in a hook chain
        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        // CallNextHookEx: passes the hook information to the next hook procedure in the current hook chain
        // Makes sure that other applications can also receive the key events
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Constants for hook type and key codes
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;

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
                // Update the label text with the key that was pressed
                lastKeyPressed.Text = ((Keys)vkCode).ToString();
            }

            // Artificial 1 second pause
            System.Threading.Thread.Sleep(1000);

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

        private void lastKeyPressed_Click(object sender, EventArgs e)
        {

        }

        private void MouseWheelActivity_Click(object sender, EventArgs e)
        {

        }
    }
}
