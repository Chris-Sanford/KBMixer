using System.Windows.Forms;

namespace KBMixer
{
    /// <summary>
    /// Maps Windows virtual key codes to short, human-readable labels for the hotkey UI.
    /// Keys not listed here use the <see cref="Keys"/> enum name (same as before).
    /// </summary>
    internal static class KeyDisplayNames
    {
        private static readonly Dictionary<int, string> FriendlyByVirtualKey = BuildMap();

        private static Dictionary<int, string> BuildMap()
        {
            var d = new Dictionary<int, string>
            {
                [(int)Keys.Back] = "Backspace",
                [(int)Keys.Tab] = "Tab",
                [(int)Keys.LineFeed] = "Line Feed",
                [(int)Keys.Clear] = "Clear",
                [(int)Keys.Enter] = "Enter",
                [(int)Keys.ShiftKey] = "Shift",
                [(int)Keys.LShiftKey] = "Shift",
                [(int)Keys.RShiftKey] = "Shift",
                [(int)Keys.ControlKey] = "Ctrl",
                [(int)Keys.LControlKey] = "Ctrl",
                [(int)Keys.RControlKey] = "Ctrl",
                [(int)Keys.Menu] = "Alt",
                [(int)Keys.LMenu] = "Alt",
                [(int)Keys.RMenu] = "Alt",
                [(int)Keys.Pause] = "Pause",
                [(int)Keys.Capital] = "Caps Lock",
                [(int)Keys.KanaMode] = "Kana",
                [(int)Keys.JunjaMode] = "Junja",
                [(int)Keys.FinalMode] = "Final",
                [(int)Keys.HanjaMode] = "Hanja",
                [(int)Keys.Escape] = "Esc",
                [(int)Keys.IMEConvert] = "IME Convert",
                [(int)Keys.IMENonconvert] = "IME Nonconvert",
                [(int)Keys.IMEAccept] = "IME Accept",
                [(int)Keys.IMEModeChange] = "IME Mode Change",
                [(int)Keys.Space] = "Space",
                [(int)Keys.Prior] = "Page Up",
                [(int)Keys.Next] = "Page Down",
                [(int)Keys.End] = "End",
                [(int)Keys.Home] = "Home",
                [(int)Keys.Left] = "Left Arrow",
                [(int)Keys.Up] = "Up Arrow",
                [(int)Keys.Right] = "Right Arrow",
                [(int)Keys.Down] = "Down Arrow",
                [(int)Keys.Select] = "Select",
                [(int)Keys.Print] = "Print",
                [(int)Keys.Execute] = "Execute",
                [(int)Keys.Snapshot] = "Print Screen",
                [(int)Keys.Insert] = "Insert",
                [(int)Keys.Delete] = "Delete",
                [(int)Keys.Help] = "Help",
                [(int)Keys.LWin] = "Win",
                [(int)Keys.RWin] = "Win",
                [(int)Keys.Apps] = "Menu",
                [(int)Keys.Sleep] = "Sleep",
                [(int)Keys.Multiply] = "Num *",
                [(int)Keys.Add] = "Num +",
                [(int)Keys.Separator] = "Num Separator",
                [(int)Keys.Subtract] = "Num -",
                [(int)Keys.Decimal] = "Num .",
                [(int)Keys.Divide] = "Num /",
                [(int)Keys.NumLock] = "Num Lock",
                [(int)Keys.Scroll] = "Scroll Lock",
                [(int)Keys.BrowserBack] = "Browser Back",
                [(int)Keys.BrowserForward] = "Browser Forward",
                [(int)Keys.BrowserRefresh] = "Browser Refresh",
                [(int)Keys.BrowserStop] = "Browser Stop",
                [(int)Keys.BrowserSearch] = "Browser Search",
                [(int)Keys.BrowserFavorites] = "Browser Favorites",
                [(int)Keys.BrowserHome] = "Browser Home",
                [(int)Keys.VolumeMute] = "Volume Mute",
                [(int)Keys.VolumeDown] = "Volume Down",
                [(int)Keys.VolumeUp] = "Volume Up",
                [(int)Keys.MediaNextTrack] = "Next Track",
                [(int)Keys.MediaPreviousTrack] = "Previous Track",
                [(int)Keys.MediaStop] = "Media Stop",
                [(int)Keys.MediaPlayPause] = "Play/Pause",
                [(int)Keys.LaunchMail] = "Mail",
                [(int)Keys.SelectMedia] = "Media Select",
                [(int)Keys.LaunchApplication1] = "App 1",
                [(int)Keys.LaunchApplication2] = "App 2",
                [(int)Keys.ProcessKey] = "Process",
                [(int)Keys.Packet] = "Packet",
                [(int)Keys.Attn] = "Attn",
                [(int)Keys.Crsel] = "Cr Sel",
                [(int)Keys.Exsel] = "Ex Sel",
                [(int)Keys.EraseEof] = "Erase EOF",
                [(int)Keys.Play] = "Play",
                [(int)Keys.Zoom] = "Zoom",
                [(int)Keys.Pa1] = "PA1",
                [(int)Keys.OemClear] = "Clear",
                [(int)Keys.OemSemicolon] = ";",
                [(int)Keys.Oemplus] = "=",
                [(int)Keys.Oemcomma] = ",",
                [(int)Keys.OemMinus] = "-",
                [(int)Keys.OemPeriod] = ".",
                [(int)Keys.OemQuestion] = "/",
                [(int)Keys.Oemtilde] = "`",
                [(int)Keys.OemOpenBrackets] = "[",
                [(int)Keys.OemPipe] = "\\",
                [(int)Keys.OemCloseBrackets] = "]",
                [(int)Keys.OemQuotes] = "'",
                [(int)Keys.Oem8] = "OEM 8",
                [(int)Keys.OemBackslash] = "\\",
            };

            for (int i = 0; i <= 9; i++)
                d[(int)Keys.NumPad0 + i] = $"Num {i}";

            for (int i = 1; i <= 24; i++)
                d[(int)Keys.F1 + (i - 1)] = $"F{i}";

            return d;
        }

        public static string GetDisplayName(int virtualKey)
        {
            if (FriendlyByVirtualKey.TryGetValue(virtualKey, out string? friendly))
                return friendly;

            return ((Keys)virtualKey).ToString();
        }
    }
}
