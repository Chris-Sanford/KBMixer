using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;

namespace KBMixer;

public partial class App : Application
{
    Window? m_window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cmdArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
        ParseUiGoldenArgs(cmdArgs);

        if (cmdArgs.Any(a => string.Equals(a, "--list-audio-sessions", StringComparison.OrdinalIgnoreCase)))
        {
            string path = Audio.WriteSessionDiagnosticFile();
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { }
            Environment.Exit(0);
            return;
        }

        bool startMinimized = !UiGoldenCapture.Enabled && cmdArgs.Any(a =>
            string.Equals(a, StartupRegistration.MinimizedCommandLineFlag, StringComparison.OrdinalIgnoreCase));

        m_window = new MainWindow(startMinimized);
        m_window.Activate();
    }

    static void ParseUiGoldenArgs(string[] args)
    {
        foreach (var a in args)
        {
            if (string.Equals(a, "--ui-golden", StringComparison.OrdinalIgnoreCase))
            {
                UiGoldenCapture.Enabled = true;
                continue;
            }

            const string prefix = "--ui-golden=";
            if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                UiGoldenCapture.Enabled = true;
                var path = a[prefix.Length..].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(path))
                    UiGoldenCapture.OutputPath = Path.GetFullPath(path);
            }
        }
    }
}
