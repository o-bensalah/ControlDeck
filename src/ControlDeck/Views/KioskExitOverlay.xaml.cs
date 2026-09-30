using System.Windows;

namespace ControlDeck.Views;

public partial class KioskExitOverlay : Window
{
    public event EventHandler? ExitRequested;

    public KioskExitOverlay()
    {
        InitializeComponent();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
}
