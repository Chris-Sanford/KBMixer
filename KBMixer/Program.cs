using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace KBMixer;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // WinUI initializes before App() is constructed. Set this before Application.Start so
        // unpackaged single-file builds resolve Windows App SDK assets from the extraction directory.
        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
            AppContext.BaseDirectory);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
