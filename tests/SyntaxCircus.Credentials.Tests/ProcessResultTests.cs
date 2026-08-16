namespace SyntaxCircus.Credentials.Tests;

public class ProcessResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var result = new ProcessResult(0, "out", "err", TimedOut: true);

        result.ExitCode.ShouldBe(0);
        result.StdOut.ShouldBe("out");
        result.StdErr.ShouldBe("err");
        result.TimedOut.ShouldBeTrue();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ProcessResult(0, "out", "err", TimedOut: false);
        var b = new ProcessResult(0, "out", "err", TimedOut: false);

        a.ShouldBe(b);
    }
}
