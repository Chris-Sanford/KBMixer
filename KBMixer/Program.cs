using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KBMixer
{
    public class VolumeControl
    {
        const int APPCOMMAND_VOLUME_MUTE = 0x80000;
        const int APPCOMMAND_VOLUME_UP = 0xA0000;
        const int APPCOMMAND_VOLUME_DOWN = 0x90000;
        const int WM_APPCOMMAND = 0x319;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public static IntPtr GetTaskbarHandle()
        {
            return FindWindow("Shell_TrayWnd", null);
        }

        public void VolumeUp(IntPtr handle)
        {
            SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_UP);
        }

        public void VolumeDown(IntPtr handle)
        {
            SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_DOWN);
        }

        public void Mute(IntPtr handle)
        {
            SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_MUTE);
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            var handle = VolumeControl.GetTaskbarHandle();
            var volumeControl = new VolumeControl();

            // Test the volume control methods
            volumeControl.VolumeUp(handle);
            volumeControl.VolumeDown(handle);
            volumeControl.Mute(handle);

            //Console.WriteLine("Press any key to exit...");
            //Console.ReadKey();
        }
    }
}
