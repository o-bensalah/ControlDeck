using System.Windows;
using System.Windows.Controls;
using ControlDeck.Services;
using Microsoft.Web.WebView2.Core;

namespace ControlDeck.Views;

public partial class StreamingPage : UserControl, IDisposable
{
    public StreamingPage()
    {
        InitializeComponent();
        Browser.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;

        foreach (var service in StreamingServiceCatalog.Load())
        {
            var button = new Button
            {
                Content = service.Name,
                Style = (Style)FindResource("DeckButtonStyle"),
                Margin = new Thickness(12),
            };
            button.Click += (_, _) => OpenService(service.Name, service.Url);
            ServicesGrid.Children.Add(button);
        }
    }

    private void OpenService(string name, string url)
    {
        CurrentServiceText.Text = name;
        PickerScroll.Visibility = Visibility.Collapsed;
        BrowserGrid.Visibility = Visibility.Visible;
        Browser.Source = new Uri(url);
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        BrowserGrid.Visibility = Visibility.Collapsed;
        PickerScroll.Visibility = Visibility.Visible;
    }

    private void SiteBack_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack) Browser.GoBack();
    }

    private void OnCoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            CurrentServiceText.Text = "WebView2 Runtime not installed";
            return;
        }

        // Block popups (ad windows, window.open()) outright — there's no window chrome to put
        // them in anyway, and streaming sites are notorious for spawning ad popups.
        Browser.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;

        // Domain-based ad/tracker blocking, done natively via WebView2's request filtering
        // rather than a browser extension — stays fully embedded, no separate process to fight
        // with for window focus like the Firefox route did.
        Browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        Browser.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) || !AdBlockList.IsBlocked(uri.Host)) return;

        e.Response = Browser.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked", "");
    }

    public void Dispose() => Browser.Dispose();
}
