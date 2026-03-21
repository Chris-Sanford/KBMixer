using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.UI.Xaml.Media;

namespace KBMixer;

/// <summary>Resolves small icons for processes (WinUI 3 <see cref="ImageSource"/>).</summary>
internal static class ProcessIconHelper
{
    public static ImageSource? TryGetIconForAudioApp(AudioApp app)
    {
        if (app.Sessions.Count == 0)
            return null;

        var first = app.Sessions[0];
        try
        {
            if (first.IsSystemSoundsSession)
                return null;
        }
        catch
        {
            return null;
        }

        try
        {
            uint pid = first.GetProcessID;
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
}
