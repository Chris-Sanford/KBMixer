using Windows.System;

namespace KBMixer;

/// <summary>
/// Maps Windows virtual key codes to short, human-readable labels for the hotkey UI.
/// Uses <see cref="VirtualKey"/> (WinRT) instead of WinForms Keys enum.
/// </summary>
internal static class KeyDisplayNames
{
    private static readonly Dictionary<int, string> FriendlyByVirtualKey = BuildMap();

    private static Dictionary<int, string> BuildMap()
    {
        var d = new Dictionary<int, string>
        {
            [(int)VirtualKey.Back] = "Backspace",
            [(int)VirtualKey.Tab] = "Tab",
            [(int)VirtualKey.Clear] = "Clear",
            [(int)VirtualKey.Enter] = "Enter",
            [(int)VirtualKey.Shift] = "Shift",
            [(int)VirtualKey.LeftShift] = "Shift",
            [(int)VirtualKey.RightShift] = "Shift",
            [(int)VirtualKey.Control] = "Ctrl",
            [(int)VirtualKey.LeftControl] = "Ctrl",
            [(int)VirtualKey.RightControl] = "Ctrl",
            [(int)VirtualKey.Menu] = "Alt",
            [(int)VirtualKey.LeftMenu] = "Alt",
            [(int)VirtualKey.RightMenu] = "Alt",
            [(int)VirtualKey.Pause] = "Pause",
            [(int)VirtualKey.CapitalLock] = "Caps Lock",
            [(int)VirtualKey.Kana] = "Kana",
            [(int)VirtualKey.Junja] = "Junja",
            [(int)VirtualKey.Final] = "Final",
            [(int)VirtualKey.Hanja] = "Hanja",
            [(int)VirtualKey.Escape] = "Esc",
            [(int)VirtualKey.Convert] = "IME Convert",
            [(int)VirtualKey.NonConvert] = "IME Nonconvert",
            [(int)VirtualKey.Accept] = "IME Accept",
            [(int)VirtualKey.ModeChange] = "IME Mode Change",
            [(int)VirtualKey.Space] = "Space",
            [(int)VirtualKey.PageUp] = "Page Up",
            [(int)VirtualKey.PageDown] = "Page Down",
            [(int)VirtualKey.End] = "End",
            [(int)VirtualKey.Home] = "Home",
            [(int)VirtualKey.Left] = "Left Arrow",
            [(int)VirtualKey.Up] = "Up Arrow",
            [(int)VirtualKey.Right] = "Right Arrow",
            [(int)VirtualKey.Down] = "Down Arrow",
            [(int)VirtualKey.Select] = "Select",
            [(int)VirtualKey.Print] = "Print",
            [(int)VirtualKey.Execute] = "Execute",
            [(int)VirtualKey.Snapshot] = "Print Screen",
            [(int)VirtualKey.Insert] = "Insert",
            [(int)VirtualKey.Delete] = "Delete",
            [(int)VirtualKey.Help] = "Help",
            [(int)VirtualKey.LeftWindows] = "Win",
            [(int)VirtualKey.RightWindows] = "Win",
            [(int)VirtualKey.Application] = "Menu",
            [(int)VirtualKey.Sleep] = "Sleep",
            [(int)VirtualKey.Multiply] = "Num *",
            [(int)VirtualKey.Add] = "Num +",
            [(int)VirtualKey.Separator] = "Num Separator",
            [(int)VirtualKey.Subtract] = "Num -",
            [(int)VirtualKey.Decimal] = "Num .",
            [(int)VirtualKey.Divide] = "Num /",
            [(int)VirtualKey.NumberKeyLock] = "Num Lock",
            [(int)VirtualKey.Scroll] = "Scroll Lock",
            [(int)VirtualKey.GoBack] = "Browser Back",
            [(int)VirtualKey.GoForward] = "Browser Forward",
            [(int)VirtualKey.Refresh] = "Browser Refresh",
            [(int)VirtualKey.Stop] = "Browser Stop",
            [(int)VirtualKey.Search] = "Browser Search",
            [(int)VirtualKey.Favorites] = "Browser Favorites",
            [(int)VirtualKey.GoHome] = "Browser Home",
            [0xAD] = "Volume Mute",
            [0xAE] = "Volume Down",
            [0xAF] = "Volume Up",
            [0xB0] = "Next Track",
            [0xB1] = "Previous Track",
            [0xB2] = "Media Stop",
            [0xB3] = "Play/Pause",
            [0xB4] = "Mail",
            [0xB5] = "Media Select",
            [0xB6] = "App 1",
            [0xB7] = "App 2",
            // OEM keys (raw VK codes since VirtualKey doesn't name all of them)
            [0xBA] = ";",
            [0xBB] = "=",
            [0xBC] = ",",
            [0xBD] = "-",
            [0xBE] = ".",
            [0xBF] = "/",
            [0xC0] = "`",
            [0xDB] = "[",
            [0xDC] = "\\",
            [0xDD] = "]",
            [0xDE] = "'",
            [0xDF] = "OEM 8",
            [0xE2] = "\\",
        };

        for (int i = 0; i <= 9; i++)
            d[(int)VirtualKey.NumberPad0 + i] = $"Num {i}";

        for (int i = 1; i <= 24; i++)
            d[(int)VirtualKey.F1 + (i - 1)] = $"F{i}";

        return d;
    }

    public static string GetDisplayName(int virtualKey)
    {
        if (FriendlyByVirtualKey.TryGetValue(virtualKey, out string? friendly))
            return friendly;

        return ((VirtualKey)virtualKey).ToString();
    }
}
