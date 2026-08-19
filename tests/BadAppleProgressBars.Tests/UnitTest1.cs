namespace BadAppleProgressBars.Tests;

public class ProjectSkeletonTests
{
    [Fact]
    public void WpfApplicationAssemblyIsReferenced()
    {
        Assert.Equal("BadAppleProgressBars", typeof(MainWindow).Assembly.GetName().Name);
    }
}
