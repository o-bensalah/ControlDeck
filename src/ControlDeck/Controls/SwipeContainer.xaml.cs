using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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
    private const double EdgeRevealZone = 60;
    // Off-screen sentinel — Point's default (0,0) would sit inside the left reveal zone, showing
    // an arrow on startup before any real pointer activity.
    private static readonly Point OffscreenPointer = new(-1000, -1000);

    private readonly List<FrameworkElement> _pages = new();
    private readonly List<Ellipse> _dots = new();
    private int _currentIndex;
    private double _dragStartOffset;
    private Point _lastPointerPosition = OffscreenPointer;

    private bool _mouseDown;
    private bool _mouseDragging;
    private Point _mouseDownPosition;

    private DependencyObject? _touchDownSource;
    private bool _manipulationDragging;

    public SwipeContainer()
    {
        InitializeComponent();
        SizeChanged += (_, _) => LayoutPages();
        // Not set via the XAML PreviewTouchDown attribute — that's already taken by
        // OnTouchMoveForEdgeReveal, and WPF allows multiple handlers on the same routed event via
        // AddHandler, so this stays a separate, single-purpose handler instead of overloading that
        // one with unrelated tap-tracking logic.
        AddHandler(PreviewTouchDownEvent, new EventHandler<TouchEventArgs>(OnTouchDownForTap), true);
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

        StopAnimationAndFreeze();
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

        UpdateArrowReveal();
    }

    // Hidden by default, like MainWindow's close button — only fades in when the pointer is near
    // the edge it would swipe from AND a page actually exists in that direction. Re-evaluated on
    // every pointer move (so moving away hides it again) and whenever the page changes (so
    // reaching the first/last page hides the now-irrelevant arrow even if the pointer hasn't moved).
    private void UpdateArrowReveal()
    {
        bool nearLeft = _lastPointerPosition.X >= 0 && _lastPointerPosition.X <= EdgeRevealZone;
        bool nearRight = _lastPointerPosition.X >= ActualWidth - EdgeRevealZone;

        SetArrowVisible(LeftArrow, nearLeft && _currentIndex > 0);
        SetArrowVisible(RightArrow, nearRight && _currentIndex < _pages.Count - 1);
    }

    // NavArrowStyle's resting look is 0.55 opacity (not fully opaque even when shown) — fade
    // between that and 0 instead of snapping Visibility, matching the fade used elsewhere in the
    // app (close button, play/pause).
    private static void SetArrowVisible(Button arrow, bool visible)
    {
        double target = visible ? 0.55 : 0;
        if (arrow.Opacity == target) return;

        arrow.IsHitTestVisible = visible;
        arrow.BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(180)));
    }

    private void OnMouseMoveForEdgeReveal(object sender, MouseEventArgs e)
    {
        _lastPointerPosition = e.GetPosition(this);
        UpdateArrowReveal();
    }

    private void OnMouseLeaveForEdgeReveal(object sender, MouseEventArgs e)
    {
        _lastPointerPosition = OffscreenPointer;
        UpdateArrowReveal();
    }

    // Touch has no hover — "near the edge" only makes sense while a finger is actually down, so
    // lifting it (PreviewTouchUp) hides the arrow again rather than leaving it stuck visible.
    private void OnTouchMoveForEdgeReveal(object sender, TouchEventArgs e)
    {
        _lastPointerPosition = e.GetTouchPoint(this).Position;
        UpdateArrowReveal();
    }

    private void OnTouchUpForEdgeReveal(object sender, TouchEventArgs e)
    {
        _lastPointerPosition = OffscreenPointer;
        UpdateArrowReveal();
    }

    // BeginAnimation(dp, null) doesn't freeze the transform at its current animated position —
    // it reverts to the last *locally set* value, which for an element only ever moved by
    // animation is stale (stuck wherever it was before the animation started, e.g. 0 from
    // startup). Read the live animated value first and re-assert it as the local value so
    // interrupting a transition (a new drag, a click mid-swipe) doesn't snap back to page 0.
    private void StopAnimationAndFreeze()
    {
        double current = PageTransform.X;
        PageTransform.BeginAnimation(TranslateTransform.XProperty, null);
        PageTransform.X = current;
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

    // IsManipulationEnabled on this container captures every touch that lands anywhere inside it
    // (including on a child Button) for the manipulation processor — unlike mouse, a touch never
    // gets to promote into a normal click on the element underneath. Sliders own their drag
    // gesture entirely (same reasoning as StartsOnSlider below for mouse), so declining the
    // manipulation there lets the touch fall through to the Thumb's own native drag handling
    // instead of being swallowed for a page swipe.
    //
    // Checked against _touchDownSource (captured in OnTouchDownForTap from the real TouchDown
    // event), not e.OriginalSource here — ManipulationStartingEventArgs.OriginalSource reflects
    // the manipulation container itself, not the element actually touched, so checking it directly
    // silently never matched the Slider/Thumb and this exclusion never fired.
    private void OnManipulationStarting(object sender, ManipulationStartingEventArgs e)
    {
        if (StartsOnSlider(_touchDownSource))
        {
            e.Cancel();
            return;
        }

        e.ManipulationContainer = this;
        StopAnimationAndFreeze();
        _dragStartOffset = PageTransform.X;
        _manipulationDragging = false;
    }

    // Real fingers are never perfectly still — a plain tap still reports a few pixels of jitter
    // through DeltaManipulation. Applying every tick directly (the old approach) made the page
    // visibly wobble on every tap, even though it always snapped back once the gesture completed
    // and turned out not to be a real swipe. Gated behind CumulativeManipulation crossing
    // DragThreshold instead, same as the mouse path below — nothing moves until a drag is
    // confirmed, then it jumps straight to the correct position from the total delta rather than
    // incrementally re-applying jitter.
    private void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
    {
        if (_pages.Count == 0) return;

        if (!_manipulationDragging)
        {
            if (Math.Abs(e.CumulativeManipulation.Translation.X) < DragThreshold) return;
            _manipulationDragging = true;
        }

        PageTransform.X = ClampDragX(_dragStartOffset + e.CumulativeManipulation.Translation.X);
    }

    private void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
    {
        // TotalManipulation is the cumulative movement across the whole gesture — under
        // DragThreshold means this was a tap, not a swipe, so replay it as a click on whatever was
        // actually touched (captured in OnTouchDownForTap) since the manipulation capture ate the
        // touch before it could promote into one on its own.
        bool wasTap = Math.Abs(e.TotalManipulation.Translation.X) < DragThreshold
            && Math.Abs(e.TotalManipulation.Translation.Y) < DragThreshold;

        FinishDrag(_dragStartOffset, e.FinalVelocities.LinearVelocity.X);
        _manipulationDragging = false;

        if (wasTap) SimulateTapClick(_touchDownSource);
    }

    private void OnTouchDownForTap(object? sender, TouchEventArgs e)
        => _touchDownSource = e.OriginalSource as DependencyObject;

    private static void SimulateTapClick(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is ButtonBase { IsEnabled: true } button)
            {
                // RaiseEvent(ClickEvent) alone only mimics a plain Button. ToggleButton's actual
                // IsChecked flip happens inside its overridden OnClick(), which a bare routed-event
                // raise never reaches.
                //
                // IToggleProvider.Toggle() fixes that (it calls ToggleButton.OnToggle() directly)
                // but has the opposite gap: OnToggle() flips IsChecked without raising Click at
                // all, so a Click handler that applies a real side effect from the new IsChecked
                // (MuteButton_Click calling into AudioService, MicMuteButton_Click into
                // MicrophoneService) never runs — IsChecked visibly flips (the X appears) but
                // nothing is actually muted, and dependent UI like the wave icons never refreshes.
                // Firing both, Toggle() then the Click event, covers both halves: correct
                // IsChecked, and the handler that acts on it.
                var peer = UIElementAutomationPeer.CreatePeerForElement(button);
                if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggleProvider)
                {
                    toggleProvider.Toggle();
                    button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
                }
                else if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invokeProvider)
                {
                    invokeProvider.Invoke();
                }
                else
                {
                    button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
                }
                return;
            }
        }
    }

    // Manipulation events only fire for touch/stylus input, not mouse — this parallel path
    // makes swiping testable (and usable) with a mouse, without breaking clicks on buttons/
    // sliders underneath: dragging only "engages" once the pointer moves past DragThreshold.
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Sliders own their drag gesture entirely — any horizontal drift while dragging the
        // thumb would otherwise get mistaken for a page swipe and steal mouse capture mid-drag.
        // Buttons don't have a competing drag gesture, so they stay swipeable: a tap still
        // clicks normally (never crosses DragThreshold), a real drag pans pages instead.
        if (StartsOnSlider(e.OriginalSource as DependencyObject))
        {
            _mouseDown = false;
            return;
        }

        _mouseDown = true;
        _mouseDragging = false;
        _mouseDownPosition = e.GetPosition(this);
        StopAnimationAndFreeze();
        _dragStartOffset = PageTransform.X;
    }

    private static bool StartsOnSlider(DependencyObject? source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Thumb or Slider) return true;
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
        // LostMouseCapture bubbles up from whichever element actually lost capture — if a child
        // Button had it (it captures on press to track its own pressed state) and we just took
        // capture away to start a swipe, that Button's LostMouseCapture bubbles through us too.
        // Only reset our drag state when we ourselves are the one who lost capture.
        if (!ReferenceEquals(e.OriginalSource, this)) return;

        _mouseDown = false;
        _mouseDragging = false;
    }

    private void OnLeftArrowClick(object sender, RoutedEventArgs e) => GoToPage(_currentIndex - 1);
    private void OnRightArrowClick(object sender, RoutedEventArgs e) => GoToPage(_currentIndex + 1);
}
