using System.Windows;
using ControlDeck.Services;
using ControlDeck.Views;

namespace ControlDeck;

public partial class MainWindow : Window
{
    private readonly ShortcutsPage _shortcutsPage = new();
    private readonly MetricsPage _metricsPage = new();
    private readonly WallpaperPage _wallpaperPage = new();

    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => KioskWindowPlacementService.PlaceOnTargetScreen(this);
        Loaded += (_, _) =>
        {
            Deck.AddPage(_shortcutsPage);
            Deck.AddPage(_metricsPage);
            Deck.AddPage(_wallpaperPage);
            Deck.GoToPage(0, animate: false);
        };
        Closed += (_, _) =>
        {
            _shortcutsPage.Dispose();
            _metricsPage.Dispose();
        };
    }
}
