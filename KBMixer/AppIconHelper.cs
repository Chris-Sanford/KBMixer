using System.Drawing;

namespace KBMixer;

/// <summary>Loads the application icon from the built .exe (PE icon) or <c>KBMixer.ico</c> beside the exe.</summary>
internal static class AppIconHelper
{
    public static void ApplyApplicationIcon(Form form, NotifyIcon? notifyIcon = null)
    {
        ArgumentNullException.ThrowIfNull(form);

        using var source = TryLoadSourceIcon();
        if (source is null)
            return;

        form.Icon = new Icon(source, source.Size);
        if (notifyIcon is not null)
            notifyIcon.Icon = new Icon(source, source.Size);
    }

    private static Icon? TryLoadSourceIcon()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                using var fromExe = Icon.ExtractAssociatedIcon(exe);
                if (fromExe is not null)
                    return new Icon(fromExe, fromExe.Size);
            }
        }
        catch
        {
            // Design-time or unusual hosts
        }

        try
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "KBMixer.ico");
            if (File.Exists(ico))
                return new Icon(ico);
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
