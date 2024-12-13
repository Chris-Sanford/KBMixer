namespace KBMixer
{
    // KXMixerConfig represents the configuration of the KBMixer application.
    // It is designed to be serialized and deserialized to and from a JSON file
    // and saved and loaded from disk.
    public class Config
    {
        public Guid ConfigId { get; set; }
        public required string DeviceId { get; set; }
        public required string AppFileName { get; set; }
        public required int[] Hotkeys { get; set; } // Array to allow required hotkey combinations
        public bool ControlSingleSession { get; set; } // Default is false, to control all sessions of same app
        public int ProcessIndex { get; set; } // if ControlSingleSession is true, this is the index of the session to control
    }
}
