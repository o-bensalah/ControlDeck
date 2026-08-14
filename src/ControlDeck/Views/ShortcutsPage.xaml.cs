using System.Windows;
using System.Windows.Controls;
using ControlDeck.Services;

namespace ControlDeck.Views;

public partial class ShortcutsPage : UserControl, IDisposable
{
    private readonly AudioService _audio = new();
    private bool _suppressSliderEvent;

    public ShortcutsPage()
    {
        InitializeComponent();
        VolumeSlider.Value = _audio.Volume * 100;
        MuteButton.IsChecked = _audio.IsMuted;
        _audio.VolumeChanged += OnSystemVolumeChanged;
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

    private void PrintScreen_Click(object sender, RoutedEventArgs e) => SystemActionsService.PrintScreen();
    private void ShowDesktop_Click(object sender, RoutedEventArgs e) => SystemActionsService.ShowDesktop();
    private void Lock_Click(object sender, RoutedEventArgs e) => SystemActionsService.Lock();
    private void Sleep_Click(object sender, RoutedEventArgs e) => SystemActionsService.Sleep();
    private void TaskManager_Click(object sender, RoutedEventArgs e) => SystemActionsService.OpenTaskManager();
    private void FileExplorer_Click(object sender, RoutedEventArgs e) => SystemActionsService.OpenFileExplorer();

    public void Dispose()
    {
        _audio.VolumeChanged -= OnSystemVolumeChanged;
        _audio.Dispose();
    }
}
