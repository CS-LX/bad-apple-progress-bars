using System.Windows;
using BadAppleProgressBars.Rendering;

namespace BadAppleProgressBars;

/// <summary>
/// Startup-only dialog. The player window itself remains Canvas and ProgressBars only.
/// </summary>
public partial class StyleSelectionWindow : Window
{
    public StyleSelectionWindow(ProgressBarAppearance initialAppearance)
    {
        InitializeComponent();
        SelectedAppearance = initialAppearance;

        switch (initialAppearance)
        {
            case ProgressBarAppearance.Flat:
                FlatOption.IsChecked = true;
                break;
            case ProgressBarAppearance.Striped:
                StripedOption.IsChecked = true;
                break;
            case ProgressBarAppearance.Aero:
                AeroOption.IsChecked = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(initialAppearance));
        }
    }

    public ProgressBarAppearance SelectedAppearance { get; private set; }

    private void OnPlayClick(object sender, RoutedEventArgs e)
    {
        SelectedAppearance = FlatOption.IsChecked == true
            ? ProgressBarAppearance.Flat
            : StripedOption.IsChecked == true
                ? ProgressBarAppearance.Striped
                : ProgressBarAppearance.Aero;
        DialogResult = true;
    }
}
