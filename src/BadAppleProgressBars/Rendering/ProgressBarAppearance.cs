namespace BadAppleProgressBars.Rendering;

/// <summary>
/// The supported official-WPF progress-bar appearances.
/// </summary>
public enum ProgressBarAppearance
{
    Flat,
    Striped,
    Aero,
}

/// <summary>
/// Resolves the XAML resource key for a selectable progress-bar appearance.
/// </summary>
public static class ProgressBarAppearanceResources
{
    public static string GetStyleKey(ProgressBarAppearance appearance) => appearance switch
    {
        ProgressBarAppearance.Flat => "FlatProgressBarStyle",
        ProgressBarAppearance.Striped => "StripedProgressBarStyle",
        ProgressBarAppearance.Aero => "AeroProgressBarStyle",
        _ => throw new ArgumentOutOfRangeException(nameof(appearance)),
    };
}
