using NAudio.CoreAudioApi;

namespace KBMixerWinForm
{
    public class KBMixerSession
    {
        public string AppName { get; set; }
        public AudioSessionControl[] Sessions { get; set; }

        public KBMixerSession(string appName, AudioSessionControl[] sessions)
        {
            AppName = appName;
            Sessions = sessions; // Is there a way to make this a reference and not a copy?
            // ^ otherwise, we can't use the KBMixer object to control the volume of the sessions
        }
    }
}
