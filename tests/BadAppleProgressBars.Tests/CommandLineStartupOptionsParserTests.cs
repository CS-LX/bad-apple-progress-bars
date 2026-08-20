using BadAppleProgressBars.Rendering;
using BadAppleProgressBars.Startup;

namespace BadAppleProgressBars.Tests;

public class CommandLineStartupOptionsParserTests
{
    [Fact]
    public void Parse_WithoutArguments_RequestsTheInteractiveStartupFlow()
    {
        var options = CommandLineStartupOptionsParser.Parse([]);

        Assert.True(options.IsInteractiveLaunch);
        Assert.Null(options.Appearance);
    }

    [Fact]
    public void Parse_FileAndNamedStyle_AcceptsEitherArgumentOrder()
    {
        var expectedPath = Path.GetFullPath("video.mp4");

        var beforeFile = CommandLineStartupOptionsParser.Parse(["--style", "striped", "video.mp4"]);
        var afterFile = CommandLineStartupOptionsParser.Parse(["video.mp4", "--style=aero"]);

        Assert.Equal(expectedPath, beforeFile.InputPath);
        Assert.Equal(ProgressBarAppearance.Striped, beforeFile.Appearance);
        Assert.Equal(expectedPath, afterFile.InputPath);
        Assert.Equal(ProgressBarAppearance.Aero, afterFile.Appearance);
    }

    [Theory]
    [InlineData("--style", "unknown")]
    [InlineData("--unknown")]
    [InlineData("first.mp4", "second.mp4")]
    public void Parse_InvalidArguments_ThrowsClearError(params string[] arguments)
    {
        var exception = Assert.Throws<ArgumentException>(() => CommandLineStartupOptionsParser.Parse(arguments));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }
}
