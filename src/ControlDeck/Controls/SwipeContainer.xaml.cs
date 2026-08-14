using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ControlDeck.Controls;

public partial class SwipeContainer : UserControl
{
    private const double DragThreshold = 10;

    private readonly List<FrameworkElement> _pages = new();
    private readonly List<Ellipse> _dots = new();
    private int _currentIndex;
    private double _dragStartOffset;

    private bool _mouseDown;
    private bool _mouseDragging;
    private Point _mouseDownPosition;

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

    private double ClampDragX(double x)
    {
        double min = -(_pages.Count - 1) * ActualWidth;
        // Allow a small rubber-band overscroll past the first/last page for touch feel.
        return Math.Clamp(x, min - 80, 80);
    }

    private void FinishDrag(double startOffset, double velocityX)
    {
        double dragged = PageTransform.X - startOffset;

        int delta = 0;
        if (dragged < -ActualWidth * 0.2 || velocityX < -300) delta = 1;
        else if (dragged > ActualWidth * 0.2 || velocityX > 300) delta = -1;

        GoToPage(_currentIndex + delta);
    }

    private void OnManipulationStarting(object sender, ManipulationStartingEventArgs e)
    {
        e.ManipulationContainer = this;
        PageTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _dragStartOffset = PageTransform.X;
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
    {
        if (_pages.Count == 0) return;
        PageTransform.X = ClampDragX(PageTransform.X + e.DeltaManipulation.Translation.X);
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        => FinishDrag(_dragStartOffset, e.FinalVelocities.LinearVelocity.X);

    // Manipulation events only fire for touch/stylus input, not mouse — this parallel path
    // makes swiping testable (and usable) with a mouse, without breaking clicks on buttons/
    // sliders underneath: dragging only "engages" once the pointer moves past DragThreshold.
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't hijack gestures that start on an interactive control (slider thumb, buttons) —
        // otherwise dragging the volume slider with any horizontal drift steals mouse capture
        // away from it mid-drag.
        if (StartsOnInteractiveControl(e.OriginalSource as DependencyObject))
        {
            _mouseDown = false;
            return;
        }

        _mouseDown = true;
        _mouseDragging = false;
        _mouseDownPosition = e.GetPosition(this);
        PageTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _dragStartOffset = PageTransform.X;
    }

    private static bool StartsOnInteractiveControl(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Thumb or ButtonBase or Slider) return true;
        }
        return false;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_mouseDown || e.LeftButton != MouseButtonState.Pressed || _pages.Count == 0) return;

        double delta = e.GetPosition(this).X - _mouseDownPosition.X;

        if (!_mouseDragging)
        {
            if (Math.Abs(delta) < DragThreshold) return;
            _mouseDragging = true;
            CaptureMouse();
        }

        PageTransform.X = ClampDragX(_dragStartOffset + delta);
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_mouseDragging)
        {
            ReleaseMouseCapture();
            FinishDrag(_dragStartOffset, 0);
            e.Handled = true;
        }

        _mouseDown = false;
        _mouseDragging = false;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _mouseDown = false;
        _mouseDragging = false;
    }

    private void OnLeftArrowClick(object sender, RoutedEventArgs e) => GoToPage(_currentIndex - 1);
    private void OnRightArrowClick(object sender, RoutedEventArgs e) => GoToPage(_currentIndex + 1);
}
