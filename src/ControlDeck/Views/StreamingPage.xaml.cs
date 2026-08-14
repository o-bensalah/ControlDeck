using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace ControlDeck.Views;

public partial class StreamingPage : UserControl, IDisposable
{
    public StreamingPage()
    {
        InitializeComponent();
        Browser.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
    }

    private void StreamOne_Click(object sender, RoutedEventArgs e) => OpenService("Cinejoy", "https://cinejoy.to/");
    private void StreamTwo_Click(object sender, RoutedEventArgs e) => OpenService("Rive", "https://www.rivestream.app/");
    private void StreamThree_Click(object sender, RoutedEventArgs e) => OpenService("NTV Stream", "https://ntv.cx/");
    private void StreamFour_Click(object sender, RoutedEventArgs e) => OpenService("Stream Sports", "https://streamsports99.ru/");
    private void StreamFive_Click(object sender, RoutedEventArgs e) => OpenService("LiveLive24", "https://livelive24.com/");
    private void StreamSix_Click(object sender, RoutedEventArgs e) => OpenService("Daddy Live", "https://dlhd.st//24-7-channels.php");

    private void OpenService(string name, string url)
    {
        CurrentServiceText.Text = name;
        PickerGrid.Visibility = Visibility.Collapsed;
        BrowserGrid.Visibility = Visibility.Visible;
        Browser.Source = new Uri(url);
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        BrowserGrid.Visibility = Visibility.Collapsed;
        PickerGrid.Visibility = Visibility.Visible;
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
        }
    }

    public void Dispose() => Browser.Dispose();
}
