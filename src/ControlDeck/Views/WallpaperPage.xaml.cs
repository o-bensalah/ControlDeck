using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ControlDeck.Views;

public partial class WallpaperPage : UserControl
{
    private static readonly string WallpaperPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControlDeck", "wallpaper.jpg");

    private readonly DispatcherTimer _clockTimer;

    public WallpaperPage()
    {
        InitializeComponent();
        ApplyWallpaper();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClock();
        Loaded += (_, _) =>
        {
            UpdateClock();
            _clockTimer.Start();
        };
        Unloaded += (_, _) => _clockTimer.Stop();
    }

    // "T" (long time pattern) follows the current culture's 12/24-hour convention, which
    // reflects the Windows time format setting — a hardcoded "HH:mm:ss" would show 24-hour
    // time even when the taskbar clock is set to 12-hour, making it look hours off.
    private void UpdateClock() => ClockText.Text = DateTime.Now.ToString("T", CultureInfo.CurrentCulture);

    private void ApplyWallpaper()
    {
        if (!File.Exists(WallpaperPath)) return;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(WallpaperPath);
        bitmap.EndInit();

        WallpaperImage.Source = bitmap;
        WallpaperImage.Visibility = Visibility.Visible;
        GradientBackground.Visibility = Visibility.Collapsed;
    }
}
