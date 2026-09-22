namespace NBL.Models;

public class GameAudioSettings
{
    public bool VoipEnabled { get; set; }
    public float VoipPlaybackVolume { get; set; }
    public float VoipCaptureVolume { get; set; }
    public float VoipCaptureThreshold { get; set; }
    public bool VoipBoostEnabled { get; set; }
    public bool VoipUsePushToTalk { get; set; }
    public string Provider { get; set; } = "software";
    public string SoundQuality { get; set; } = "High";
    public float EffectsVolume { get; set; }
    public float MusicVolume { get; set; }
    public float HelpVoiceVolume { get; set; }
    public bool EnglishOnlyVoices { get; set; }
    public bool EnableEAX { get; set; }
}