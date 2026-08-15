using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ControlDeck.Services;
using ControlDeck.Views;

namespace ControlDeck;

public partial class MainWindow : Window
{
    private const double MouseRevealHalfWidth = 90;
    private const double MouseRevealHeight = 44;
    private const double TopEdgeHotZoneHeight = 36;
    private const double TouchRevealDragDistance = 40;

    private readonly List<ShortcutsPage> _shortcutsPages;
    private readonly StreamingPage _streamingPage = new();
    private readonly WallpaperPage _wallpaperPage = new();
    private readonly Dictionary<int, Point> _activeTopTouches = new();

    private Point _lastMousePosition;
    private bool _touchRevealed;
    private DispatcherTimer? _touchHideTimer;

    public MainWindow()
    {
        InitializeComponent();

        // Chunk into as many pages as the catalog needs (12 buttons each) instead of a single
        // fixed page — only the first chunk shows metrics; every chunk gets its own MediaWidget.
        // Guard against an empty catalog (e.g. the user emptied the JSON) still yielding at least
        // one page, so metrics/media controls remain reachable.
        var chunks = AppLauncherCatalog.Load().Chunk(ShortcutsPage.MaxEntriesPerPage).ToList();
        if (chunks.Count == 0) chunks.Add(Array.Empty<AppLauncherEntry>());
        _shortcutsPages = chunks
            .Select((chunk, index) => new ShortcutsPage(chunk, showMetrics: index == 0))
            .ToList();

        SourceInitialized += (_, _) => KioskWindowPlacementService.PlaceOnTargetScreen(this);
        Loaded += (_, _) =>
        {
            foreach (var page in _shortcutsPages) Deck.AddPage(page);
            Deck.AddPage(_streamingPage);
            Deck.AddPage(_wallpaperPage);
            Deck.GoToPage(0, animate: false);
        };
        Closed += (_, _) =>
        {
            foreach (var page in _shortcutsPages) page.Dispose();
            _streamingPage.Dispose();
        };
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e) => Close();

    private void OnRootMouseMove(object sender, MouseEventArgs e)
    {
        _lastMousePosition = e.GetPosition(RootGrid);
        UpdateCloseButtonVisibility();
    }

    private void OnRootPreviewTouchDown(object sender, TouchEventArgs e)
    {
        var position = e.GetTouchPoint(RootGrid).Position;
        if (position.Y <= TopEdgeHotZoneHeight)
        {
            _activeTopTouches[e.TouchDevice.Id] = position;
        }
    }

    private void OnRootPreviewTouchMove(object sender, TouchEventArgs e)
    {
        if (!_activeTopTouches.TryGetValue(e.TouchDevice.Id, out var start)) return;

        var position = e.GetTouchPoint(RootGrid).Position;
        if (position.Y - start.Y >= TouchRevealDragDistance)
        {
            _activeTopTouches.Remove(e.TouchDevice.Id);
            RevealForTouch();
        }
    }

    private void OnRootPreviewTouchUp(object sender, TouchEventArgs e) => _activeTopTouches.Remove(e.TouchDevice.Id);

    private void RevealForTouch()
    {
        _touchRevealed = true;
        UpdateCloseButtonVisibility();

        _touchHideTimer?.Stop();
        _touchHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _touchHideTimer.Tick += (_, _) =>
        {
            _touchHideTimer!.Stop();
            _touchRevealed = false;
            UpdateCloseButtonVisibility();
        };
        _touchHideTimer.Start();
    }

    private void UpdateCloseButtonVisibility()
    {
        bool mouseNearTopCenter =
            _lastMousePosition.Y >= 0 && _lastMousePosition.Y <= MouseRevealHeight &&
            Math.Abs(_lastMousePosition.X - RootGrid.ActualWidth / 2) <= MouseRevealHalfWidth;

        SetCloseButtonVisible(mouseNearTopCenter || _touchRevealed);
    }

    private void SetCloseButtonVisible(bool visible)
    {
        double target = visible ? 1 : 0;
        if (CloseButton.Opacity == target) return;

        CloseButton.IsHitTestVisible = visible;
        CloseButton.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(150)));
    }
}
