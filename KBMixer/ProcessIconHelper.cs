using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.UI.Xaml.Media;
using NAudio.CoreAudioApi;

namespace KBMixer;

/// <summary>Resolves small icons for processes (WinUI 3 <see cref="ImageSource"/>).</summary>
internal static class ProcessIconHelper
{
    public static ImageSource? TryGetIconForAudioApp(AudioApp app)
    {
        if (app.Sessions.Count == 0)
            return null;
        return TryGetIconForSession(app.Sessions[0]);
    }

    public static ImageSource? TryGetIconForSession(AudioSessionControl session)
    {
        try
        {
            if (session.IsSystemSoundsSession)
                return null;
        }
        catch { return null; }

        try
        {
            uint pid = session.GetProcessID;
            if (pid == 0)
                return null;

            using var proc = Process.GetProcessById((int)pid);
            string? exePath = null;
            try { exePath = proc.MainModule?.FileName; }
            catch { }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                using var sysIcon = Icon.ExtractAssociatedIcon(exePath);
                if (sysIcon is not null)
                    return AppIconHelper.ToImageSource(sysIcon, Path.GetFileNameWithoutExtension(exePath));
            }
        }
        catch { }

        return null;
    }

    /// <summary>Finds an icon for an app by friendly name from the current session list.</summary>
    public static ImageSource? TryGetIconByFriendlyName(AudioApp[] audioApps, string friendlyName)
    {
        var app = audioApps.FirstOrDefault(a =>
            string.Equals(a.AppFriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase));
        return app != null ? TryGetIconForAudioApp(app) : null;
    }
}
