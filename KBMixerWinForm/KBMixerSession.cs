using NAudio.CoreAudioApi;

namespace KBMixerWinForm
{
    public class KBMixerSession
    {
        public string BinaryName { get; set; }
        public AudioSessionControl[] Sessions { get; set; }

        public KBMixerSession(string binaryName, AudioSessionControl[] sessions)
        {
            BinaryName = binaryName;
            Sessions = sessions;
        }
    }
}
