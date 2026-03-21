using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;

namespace KBMixer;

/// <summary>One row in the mixer strip (device master or a grouped app on the current device).</summary>
public sealed class MixerChannelViewModel : INotifyPropertyChanged
{
    float _volumeScalar;
    bool _isHotkeyTarget;
    bool _isRowExpanded;

    public MixerChannelViewModel(bool isMaster, string title, ImageSource? icon, AudioApp? app, NAudio.CoreAudioApi.MMDevice? device, string? detailText = null)
    {
        IsMaster = isMaster;
        Title = title;
        DetailText = detailText;
        Icon = icon;
        App = app;
        Device = device;
    }

    public bool IsMaster { get; }
    public string Title { get; }
    public string? DetailText { get; }
    public ImageSource? Icon { get; }
    public AudioApp? App { get; }
    public NAudio.CoreAudioApi.MMDevice? Device { get; }

    public bool IsExpandable => !IsMaster;

    public bool IsRowExpanded
    {
        get => _isRowExpanded;
        set
        {
            if (_isRowExpanded == value)
                return;
            _isRowExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowExpandedPanel));
        }
    }

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

    // Computed visibility helpers — replaces WPF DataTrigger/MultiDataTrigger
    public bool ShowMasterIcon => IsMaster;
    public bool ShowFallbackIcon => !IsMaster && Icon == null;
    public bool ShowAppIcon => Icon != null;
    public bool ShowChevron => !IsMaster;
    public bool ShowExpandedPanel => !IsMaster && IsRowExpanded;
    public bool ShowDetailText => IsMaster;
    public bool ShowVolumeGlyph => !IsMaster;
    public bool ShowSetTargetButton => !IsMaster && App != null;

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
        set => VolumeScalar = (float)Math.Clamp(value / 100.0, 0, 1);
    }

    public string VolumePercentText => VolumePercent.ToString("0");

    public void SyncVolumeFromAudio(float scalar)
    {
        scalar = Math.Clamp(scalar, 0f, 1f);
        if (Math.Abs(_volumeScalar - scalar) < 0.001f)
            return;
        _volumeScalar = scalar;
        OnPropertyChanged(nameof(VolumeScalar));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(VolumePercentText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
