using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ControlDeck.Services;

namespace ControlDeck.Controls;

// Self-contained: owns its own AudioService/MediaSessionService/timer, so any page can embed one
// without touching that page's own state. Each instance polls independently — a small, acceptable
// redundancy given at most a handful of pages exist, in exchange for zero coupling between pages.
public partial class MediaWidget : UserControl, IDisposable
{
    private readonly AudioService _audio = new();
    private readonly MediaSessionService _media = new();
    private readonly DispatcherTimer _playbackTimer;
    private bool _suppressSliderEvent;

    private FrameworkElement? _wave1;
    private FrameworkElement? _wave2;
    private FrameworkElement? _wave3;

    public MediaWidget()
    {
        InitializeComponent();

        // MuteToggleStyle is a StaticResource resolved at parse time, so ApplyTemplate() here
        // (right after InitializeComponent) reliably materializes the template — these named
        // parts aren't exposed as fields since they live inside a ControlTemplate, not this
        // control's own XAML.
        MuteButton.ApplyTemplate();
        _wave1 = MuteButton.Template.FindName("Wave1", MuteButton) as FrameworkElement;
        _wave2 = MuteButton.Template.FindName("Wave2", MuteButton) as FrameworkElement;
        _wave3 = MuteButton.Template.FindName("Wave3", MuteButton) as FrameworkElement;

        VolumeSlider.Value = _audio.Volume * 100;
        MuteButton.IsChecked = _audio.IsMuted;
        UpdateSpeakerWaves();
        _audio.VolumeChanged += OnSystemVolumeChanged;

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _playbackTimer.Tick += async (_, _) => await RefreshPlaybackStateAsync();
        Loaded += (_, _) => _playbackTimer.Start();
        Unloaded += (_, _) => _playbackTimer.Stop();
    }

    private void OnSystemVolumeChanged(float level, bool muted)
    {
        Dispatcher.Invoke(() =>
        {
            _suppressSliderEvent = true;
            VolumeSlider.Value = level * 100;
            MuteButton.IsChecked = muted;
            _suppressSliderEvent = false;
            UpdateSpeakerWaves();
        });
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderEvent) return;
        _audio.Volume = (float)(e.NewValue / 100.0);
        UpdateSpeakerWaves();
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        _audio.IsMuted = MuteButton.IsChecked == true;
        UpdateSpeakerWaves();
    }

    // 0-3 sound-wave arcs depending on level, none when muted or silent — mirrors how OS volume
    // icons behave. Set as local values rather than via an XAML trigger: local values take
    // precedence over style/template triggers in WPF, so a trigger touching the same properties
    // would otherwise fight with this for control once both are in play.
    private void UpdateSpeakerWaves()
    {
        int level = _audio.IsMuted || _audio.Volume <= 0f ? 0
            : _audio.Volume < 0.34f ? 1
            : _audio.Volume < 0.67f ? 2
            : 3;

        if (_wave1 is not null) _wave1.Opacity = level >= 1 ? 1 : 0;
        if (_wave2 is not null) _wave2.Opacity = level >= 2 ? 1 : 0;
        if (_wave3 is not null) _wave3.Opacity = level >= 3 ? 1 : 0;
    }

    // The ToggleButton flips its own IsChecked (and plays the crossfade animation) immediately
    // on click, before this handler runs — the next timer tick self-corrects it if the toggle
    // didn't actually take (e.g. no active media session).
    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _media.TogglePlayPauseAsync();
    private async void PreviousTrack_Click(object sender, RoutedEventArgs e) => await _media.SkipPreviousAsync();
    private async void NextTrack_Click(object sender, RoutedEventArgs e) => await _media.SkipNextAsync();

    private async Task RefreshPlaybackStateAsync()
    {
        bool isPlaying = await _media.IsPlayingAsync();
        if (PlayPauseButton.IsChecked != isPlaying) PlayPauseButton.IsChecked = isPlaying;
    }

    public void Dispose()
    {
        _audio.VolumeChanged -= OnSystemVolumeChanged;
        _audio.Dispose();
        _playbackTimer.Stop();
    }
}
