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

    public MediaWidget()
    {
        InitializeComponent();

        VolumeSlider.Value = _audio.Volume * 100;
        MuteButton.IsChecked = _audio.IsMuted;
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
        });
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressSliderEvent) return;
        _audio.Volume = (float)(e.NewValue / 100.0);
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e) => _audio.IsMuted = MuteButton.IsChecked == true;

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
