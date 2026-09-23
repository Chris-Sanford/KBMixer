using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;
using NAudio.CoreAudioApi;

namespace KBMixer;

/// <summary>One row in the mixer strip (device master, or a per-process audio entry).</summary>
public sealed class MixerChannelViewModel : INotifyPropertyChanged
{
    float _volumeScalar;
    bool _isMuted;
    bool _isHotkeyTarget;

    public MixerChannelViewModel(
        bool isMaster, string title, ImageSource? icon,
        AudioApp? app, MMDevice? device,
        string? detailText = null,
        List<AudioSessionControl>? sessionsForRow = null,
        bool targetUnavailable = false)
    {
        IsMaster = isMaster;
        Title = title;
        DetailText = detailText;
        Icon = icon;
        App = app;
        Device = device;
        SessionsForRow = sessionsForRow ?? new();
        IsTargetUnavailable = targetUnavailable;
    }

    public bool IsMaster { get; }
    public string Title { get; }
    public string? DetailText { get; }
    public ImageSource? Icon { get; }
    public AudioApp? App { get; }
    public MMDevice? Device { get; }
    public List<AudioSessionControl> SessionsForRow { get; }
    public bool IsTargetUnavailable { get; }

    /// <summary>Tracks when the user last interacted with this row's slider to suppress sync.</summary>
    internal long LastUserInteractionTicks { get; set; }

    public bool IsHotkeyTarget
    {
        get => _isHotkeyTarget;
        set
        {
            if (_isHotkeyTarget == value)
                return;
            _isHotkeyTarget = value;
            OnPropertyChanged();
        }
    }

    public bool ShowMasterIcon => IsMaster;
    public bool ShowFallbackIcon => !IsMaster && Icon == null;
    public bool ShowAppIcon => Icon != null;
    public bool ShowDetailText => !string.IsNullOrEmpty(DetailText);
    public bool ShowVolumeGlyph => !IsMaster;
    public bool IsVolumeControlEnabled =>
        !IsTargetUnavailable &&
        (IsMaster ? Device != null : SessionsForRow.Count > 0 || (App?.Sessions.Count ?? 0) > 0);

    public bool IsMuted => _isMuted;
    public double VolumeControlOpacity => IsMuted ? 0.38 : 1.0;
    public string MuteGlyph => IsMuted ? "\uE74F" : "\uE995";
    public string MuteToolTip => !IsVolumeControlEnabled
        ? "Mute unavailable"
        : IsMaster
            ? (IsMuted ? "Unmute" : "Mute")
            : (IsMuted ? "Unmute all sessions" : "Mute all sessions");

    public float VolumeScalar
    {
        get => _volumeScalar;
        set
        {
            value = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_volumeScalar - value) < 0.0001f)
                return;
            _volumeScalar = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumePercent));
            OnPropertyChanged(nameof(VolumePercentText));
        }
    }

    public double VolumePercent
    {
        get => Math.Round(VolumeScalar * 100.0, 0);
        set
        {
            float newScalar = (float)Math.Clamp(value / 100.0, 0, 1);
            if ((int)Math.Round(newScalar * 100.0) != (int)Math.Round(_volumeScalar * 100.0))
                VolumeScalar = newScalar;
        }
    }

    public string VolumePercentText => VolumePercent.ToString("0");

    public void SyncVolumeFromAudio(float scalar)
    {
        scalar = Math.Clamp(scalar, 0f, 1f);
        if ((int)Math.Round(scalar * 100.0) == (int)Math.Round(_volumeScalar * 100.0))
            return;
        _volumeScalar = scalar;
        OnPropertyChanged(nameof(VolumeScalar));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(VolumePercentText));
    }

    public void SyncMuteFromAudio(bool muted)
    {
        if (_isMuted == muted)
            return;
        _isMuted = muted;
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(VolumeControlOpacity));
        OnPropertyChanged(nameof(MuteGlyph));
        OnPropertyChanged(nameof(MuteToolTip));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
