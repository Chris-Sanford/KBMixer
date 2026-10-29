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

    // UI thread only. One entry per executable; null means "no icon available" so it isn't retried on every rebuild.
    static readonly Dictionary<string, ImageSource?> IconsByExePath = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? TryGetIconForSession(AudioSessionControl session)
    {
        var info = Audio.Info(session);
        if (info.IsSystemSounds || info.ProcessId == 0 || string.IsNullOrEmpty(info.ExePath))
            return null;

        string exePath = info.ExePath;
        if (IconsByExePath.TryGetValue(exePath, out var cached))
            return cached;

        ImageSource? icon = null;
        try
        {
            if (File.Exists(exePath))
            {
                using var sysIcon = Icon.ExtractAssociatedIcon(exePath);
                if (sysIcon is not null)
                    icon = AppIconHelper.ToImageSource(sysIcon, Path.GetFileNameWithoutExtension(exePath));
            }
        }
        catch { }

        IconsByExePath[exePath] = icon;
        return icon;
    }

    /// <summary>Finds an icon for an app by friendly name from the current session list.</summary>
    public static ImageSource? TryGetIconByFriendlyName(AudioApp[] audioApps, string friendlyName)
    {
        var app = audioApps.FirstOrDefault(a =>
            string.Equals(a.AppFriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase));
        return app != null ? TryGetIconForAudioApp(app) : null;
    }
}
