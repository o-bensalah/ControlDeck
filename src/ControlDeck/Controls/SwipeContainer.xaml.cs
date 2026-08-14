using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ControlDeck.Controls;

public partial class SwipeContainer : UserControl
{
    private readonly List<FrameworkElement> _pages = new();
    private readonly List<Ellipse> _dots = new();
    private int _currentIndex;
    private double _manipulationStartOffset;

    public SwipeContainer()
    {
        InitializeComponent();
        SizeChanged += (_, _) => LayoutPages();
    }

    public void AddPage(FrameworkElement page)
    {
        _pages.Add(page);
        PageCanvas.Children.Add(page);

        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Margin = new Thickness(5, 0, 5, 0),
            Fill = (Brush)FindResource("DotInactiveBrush"),
        };
        _dots.Add(dot);
        DotsPanel.Children.Add(dot);

        LayoutPages();
        UpdateChrome();
    }

    public void GoToPage(int index, bool animate = true)
    {
        if (_pages.Count == 0) return;

        index = Math.Clamp(index, 0, _pages.Count - 1);
        _currentIndex = index;
        double target = -index * ActualWidth;

        PageTransform.BeginAnimation(TranslateTransform.XProperty, null);
        if (animate)
        {
            var anim = new DoubleAnimation(PageTransform.X, target, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            PageTransform.BeginAnimation(TranslateTransform.XProperty, anim);
        }
        else
        {
            PageTransform.X = target;
        }

        UpdateChrome();
    }

    private void LayoutPages()
    {
        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0 || _pages.Count == 0) return;

        PageCanvas.Width = w * _pages.Count;
        PageCanvas.Height = h;

        for (int i = 0; i < _pages.Count; i++)
        {
            _pages[i].Width = w;
            _pages[i].Height = h;
            Canvas.SetLeft(_pages[i], i * w);
            Canvas.SetTop(_pages[i], 0);
        }

        GoToPage(_currentIndex, animate: false);
    }

    private void UpdateChrome()
    {
        for (int i = 0; i < _dots.Count; i++)
        {
            _dots[i].Fill = (Brush)FindResource(i == _currentIndex ? "DotActiveBrush" : "DotInactiveBrush");
        }

        LeftArrow.Visibility = _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
        RightArrow.Visibility = _currentIndex < _pages.Count - 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnManipulationStarting(object sender, ManipulationStartingEventArgs e)
    {
        e.ManipulationContainer = this;
        PageTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _manipulationStartOffset = PageTransform.X;
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
    {
        if (_pages.Count == 0) return;

        double next = PageTransform.X + e.DeltaManipulation.Translation.X;
        double min = -(_pages.Count - 1) * ActualWidth;
        // Allow a small rubber-band overscroll past the first/last page for touch feel.
        PageTransform.X = Math.Clamp(next, min - 80, 80);
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        double dragged = PageTransform.X - _manipulationStartOffset;
        double velocity = e.FinalVelocities.LinearVelocity.X;

        int delta = 0;
        if (dragged < -ActualWidth * 0.2 || velocity < -300) delta = 1;
        else if (dragged > ActualWidth * 0.2 || velocity > 300) delta = -1;

        GoToPage(_currentIndex + delta);
    }

    private void OnLeftArrowClick(object sender, RoutedEventArgs e) => GoToPage(_currentIndex - 1);
    private void OnRightArrowClick(object sender, RoutedEventArgs e) => GoToPage(_currentIndex + 1);
}
