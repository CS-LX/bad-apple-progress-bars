using System.IO;
using BadAppleProgressBars.Rendering;

namespace BadAppleProgressBars.Startup;

/// <summary>
/// The optional file and appearance explicitly supplied on the command line.
/// </summary>
public sealed record CommandLineStartupOptions(string? InputPath, ProgressBarAppearance? Appearance)
{
    public bool IsInteractiveLaunch => InputPath is null;
}

/// <summary>
/// Parses the intentionally small player command line without opening UI.
/// </summary>
public static class CommandLineStartupOptionsParser
{
    public static CommandLineStartupOptions Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? inputPath = null;
        ProgressBarAppearance? appearance = null;
        using var enumerator = arguments.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var argument = enumerator.Current;

            if (string.IsNullOrWhiteSpace(argument))
            {
                throw new ArgumentException("Command-line arguments cannot be empty.", nameof(arguments));
            }

            if (string.Equals(argument, "--style", StringComparison.OrdinalIgnoreCase))
            {
                if (!enumerator.MoveNext())
                {
                    throw new ArgumentException("--style requires flat, striped, or aero.", nameof(arguments));
                }

                appearance = ParseAppearance(enumerator.Current);
                continue;
            }

            if (argument.StartsWith("--style=", StringComparison.OrdinalIgnoreCase))
            {
                appearance = ParseAppearance(argument[8..]);
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown option: {argument}", nameof(arguments));
            }

            if (inputPath is not null)
            {
                throw new ArgumentException("Only one input video or .bpb file may be supplied.", nameof(arguments));
            }

            inputPath = Path.GetFullPath(argument);
        }

        return new CommandLineStartupOptions(inputPath, appearance);
    }

    private static ProgressBarAppearance ParseAppearance(string value) => value.ToLowerInvariant() switch
    {
        "flat" => ProgressBarAppearance.Flat,
        "striped" => ProgressBarAppearance.Striped,
        "aero" => ProgressBarAppearance.Aero,
        _ => throw new ArgumentException("--style must be flat, striped, or aero."),
    };
}
