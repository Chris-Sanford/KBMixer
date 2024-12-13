namespace KBMixer
{
    // Contains methods to interact with the keyboard and mouse
    public class KBMInput
    {
        private const int mouseWheelMove = 120; // 120 is up, -120 is down
        private const string mouseWheelButton = "MouseWheel";
        private const string up = "Up";
        private const string down = "Down";
        private int hotkey;
        private bool listeningForHotkeySet = false;
        private bool hotkeyHeld = false;
    }
}
