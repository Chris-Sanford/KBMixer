using System.IO;
using Microsoft.Win32;

namespace KBMixer
{
    /// <summary>Registers or removes a per-user Windows logon entry under HKCU Run.</summary>
    internal static class StartupRegistration
    {
        internal const string RegistryValueName = "KBMixer";

        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Passed on the command line so logon starts go straight to the notification area.</summary>
        internal const string MinimizedCommandLineFlag = "--minimized";

        internal static bool IsRegisteredForCurrentExe()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var raw = key?.GetValue(RegistryValueName) as string;
            if (string.IsNullOrWhiteSpace(raw) || !TryParseExecutableFromRunValue(raw, out string? exe) || exe is null)
                return false;

            string? thisExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(thisExe))
                return false;
            return PathsReferToSameFile(exe, thisExe);
        }

        internal static void SetRegistered(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException(@"Could not open HKCU\Software\Microsoft\Windows\CurrentVersion\Run.");

            if (enable)
            {
                string exe = Environment.ProcessPath
                    ?? throw new InvalidOperationException("Could not determine the executable path.");
                string command = $"\"{exe}\" {MinimizedCommandLineFlag}";
                key.SetValue(RegistryValueName, command, RegistryValueKind.String);
            }
            else
                key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
        }

        internal static bool TryParseExecutableFromRunValue(string value, out string? executablePath)
        {
            executablePath = null;
            value = value.Trim();
            if (value.Length == 0)
                return false;

            if (value[0] == '"')
            {
                int end = value.IndexOf('"', 1);
                if (end <= 0)
                    return false;
                executablePath = value.Substring(1, end - 1);
                return executablePath.Length > 0;
            }

            int space = value.IndexOf(' ');
            executablePath = space < 0 ? value : value[..space];
            return executablePath.Length > 0;
        }

        static bool PathsReferToSameFile(string a, string b)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(a.Trim()),
                    Path.GetFullPath(b.Trim()),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
