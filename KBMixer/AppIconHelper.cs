using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KBMixer;

/// <summary>Loads the application icon from the built .exe (PE icon) or KBMixer.ico beside the exe.</summary>
internal static class AppIconHelper
{
    static readonly string IconCacheDir = Path.Combine(Path.GetTempPath(), "KBMixer", "icons");

    public static void ApplyWindowIcon(Microsoft.UI.Windowing.AppWindow appWindow)
    {
        var icoPath = Path.Combine(AppContext.BaseDirectory, "KBMixer.ico");
        if (File.Exists(icoPath))
            appWindow.SetIcon(icoPath);
    }

    public static Icon? TryLoadSystemDrawingIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                using var fromExe = Icon.ExtractAssociatedIcon(exe);
                if (fromExe is not null)
                    return new Icon(fromExe, fromExe.Size);
            }
        }
        catch { }

        try
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "KBMixer.ico");
            if (File.Exists(ico))
                return new Icon(ico);
        }
        catch { }

        return null;
    }

    /// <summary>Converts a System.Drawing.Icon to a WinUI 3 ImageSource via temp PNG cache.</summary>
    public static Microsoft.UI.Xaml.Media.ImageSource? ToImageSource(Icon icon, string cacheKey)
    {
        try
        {
            Directory.CreateDirectory(IconCacheDir);
            var safeName = string.Concat(cacheKey.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (string.IsNullOrEmpty(safeName)) safeName = "icon";
            var cachePath = Path.Combine(IconCacheDir, safeName + ".png");

            if (!File.Exists(cachePath))
            {
                using var bitmap = icon.ToBitmap();
                bitmap.Save(cachePath, ImageFormat.Png);
            }

            return new BitmapImage(new Uri(cachePath));
        }
        catch
        {
            return null;
        }
    }
}
