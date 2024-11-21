using System; // Import the System namespace for basic functionalities
using System.Diagnostics; // Import the System.Diagnostics namespace for debugging and diagnostics
using System.Runtime.InteropServices; // Import the System.Runtime.InteropServices namespace for interop services
// Interop services, short for "interoperability services," are a set of tools and libraries provided by the .NET framework that allow managed code (code that runs under the control of the .NET runtime, such as C#) to interact with unmanaged code (code that runs outside the .NET runtime, such as native Windows APIs or libraries written in C or C++).

/// <summary>
/// The KBMixer namespace contains classes for controlling the system volume.
/// </summary>
/// <remarks>
/// In C#, a namespace is a way to organize code and group related classes, interfaces, structs, and enums. 
/// It helps to avoid naming conflicts by providing a way to fully qualify names.
/// </remarks>
namespace KBMixer // Define a namespace called KBMixer
{
    /// <summary>
    /// Provides methods to control the system volume.
    /// </summary>
    public class VolumeControl // Define a public class called VolumeControl
    {
        const int APPCOMMAND_VOLUME_MUTE = 0x80000; // Constant for the volume mute command
        const int APPCOMMAND_VOLUME_UP = 0xA0000; // Constant for the volume up command
        const int APPCOMMAND_VOLUME_DOWN = 0x90000; // Constant for the volume down command
        const int WM_APPCOMMAND = 0x319; // Constant for the Windows message for app commands

        // The DllImport attribute is used to import a method from an unmanaged DLL (Dynamic Link Library).
        // In this case, we are importing the SendMessageW function from the user32.dll library.
        [DllImport("user32.dll")] 
        // The public keyword makes the method accessible from other classes, and static means it belongs to the class itself rather than an instance of the class.
        // This means you can call the method without creating an object of the class. Static methods are often used for operations that are relevant to all instances of a class, such as utility functions.
        // This would mean if you called the method it would affect the whole class, not just one instance of it.
        // The extern keyword indicates that the method is implemented externally, in this case, in the user32.dll.
        public static extern 
        // The return type of the SendMessageW function is IntPtr, which is a platform-specific type used to represent a pointer or a handle.
        IntPtr 
        // The name of the method we are importing. It matches the name of the function in the DLL.
        SendMessageW(
        // The first parameter is hWnd, which is a handle to the window whose window procedure will receive the message.
        IntPtr hWnd, 
        // The second parameter is Msg, which is the message to be sent.
        int Msg, 
        // The third parameter is wParam, which is additional message-specific information.
        IntPtr wParam, 
        // The fourth parameter is lParam, which is additional message-specific information.
        IntPtr lParam
        );

        [DllImport("user32.dll")] // Import the FindWindow function from user32.dll
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// Gets the handle of the taskbar.
        /// </summary>
        /// <returns>The handle of the taskbar.</returns>
        public static IntPtr GetTaskbarHandle() // Method to get the handle of the taskbar
        {
            return FindWindow("Shell_TrayWnd", null); // Find the window with class name "Shell_TrayWnd" (taskbar)
        }

        /// <summary>
        /// Increases the system volume.
        /// </summary>
        /// <param name="handle">The handle of the window to send the command to.</param>
        public void VolumeUp(IntPtr handle) // Method to increase the volume
        {
            SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_UP); // Send the volume up command
        }

        /// <summary>
        /// Decreases the system volume.
        /// </summary>
        /// <param name="handle">The handle of the window to send the command to.</param>
        public void VolumeDown(IntPtr handle) // Method to decrease the volume
        {
            SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_DOWN); // Send the volume down command
        }

        /// <summary>
        /// Mutes the system volume.
        /// </summary>
        /// <param name="handle">The handle of the window to send the command to.</param>
        /// void means it returns no value
        public void Mute(IntPtr handle) // Method to mute the volume
        {
            SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_MUTE); // Send the volume mute command
        }
    }

    /// <summary>
    /// The main entry point of the program.
    /// </summary>
    class Program // Define a class called Program
    {
        /// <summary>
        /// The main method, entry point of the program.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        static void Main(string[] args) // Main method, entry point of the program
        {
            var handle = VolumeControl.GetTaskbarHandle(); // Get the handle of the taskbar
            var volumeControl = new VolumeControl(); // Create an instance of VolumeControl

            // Test the volume control methods
            volumeControl.VolumeUp(handle); // Increase the volume
            volumeControl.VolumeDown(handle); // Decrease the volume
            volumeControl.Mute(handle); // Mute the volume

            //Console.WriteLine("Press any key to exit..."); // Commented out: Prompt user to press any key to exit
            //Console.ReadKey(); // Commented out: Wait for user input before closing
        }
    }
}
