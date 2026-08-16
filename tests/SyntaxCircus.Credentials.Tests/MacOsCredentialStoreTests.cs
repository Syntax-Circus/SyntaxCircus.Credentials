namespace SyntaxCircus.Credentials.Tests;

public class MacOsCredentialStoreTests
{
    private static IProcessRunner CreateProcessRunner(ProcessResult result)
    {
        var runner = Substitute.For<IProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns(result);
        return runner;
    }

    [Fact]
    public async Task GetAsync_ExitCodeZero_ReturnsStdOut()
    {
        var runner = CreateProcessRunner(new ProcessResult(0, "s3cr3t", string.Empty, TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBe("s3cr3t");
    }

    [Fact]
    public async Task GetAsync_NonZeroExitCode_ReturnsNull()
    {
        var runner = CreateProcessRunner(new ProcessResult(44, string.Empty, "not found", TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_UsesFindGenericPasswordCommand()
    {
        var runner = CreateProcessRunner(new ProcessResult(0, "s3cr3t", string.Empty, TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        await runner.Received(1).RunAsync(
            "security",
            Arg.Is<string>(args => args.Contains("find-generic-password", StringComparison.Ordinal) && args.Contains("-w", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task SetAsync_UsesAddGenericPasswordWithUpdateFlag()
    {
        var runner = CreateProcessRunner(new ProcessResult(0, string.Empty, string.Empty, TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);

        await runner.Received(1).RunAsync(
            "security",
            Arg.Is<string>(args => args.Contains("add-generic-password -U", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task DeleteAsync_UsesDeleteGenericPasswordCommand()
    {
        var runner = CreateProcessRunner(new ProcessResult(44, string.Empty, "not found", TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        await store.DeleteAsync("service1", "account1", TestContext.Current.CancellationToken);

        await runner.Received(1).RunAsync(
            "security",
            Arg.Is<string>(args => args.Contains("delete-generic-password", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task DeleteAsync_NonZeroExitCode_DoesNotThrow()
    {
        var runner = CreateProcessRunner(new ProcessResult(44, string.Empty, "not found", TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        await Should.NotThrowAsync(() => store.DeleteAsync("service1", "account1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistsAsync_ValueFound_ReturnsTrue()
    {
        var runner = CreateProcessRunner(new ProcessResult(0, "s3cr3t", string.Empty, TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        (await store.ExistsAsync("service1", "account1", TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ValueNotFound_ReturnsFalse()
    {
        var runner = CreateProcessRunner(new ProcessResult(44, string.Empty, string.Empty, TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        (await store.ExistsAsync("service1", "account1", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_ServiceIdContainsSingleQuote_EscapesForShell()
    {
        string? capturedArgs = null;
        var runner = Substitute.For<IProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Do<string>(args => capturedArgs = args), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns(new ProcessResult(0, string.Empty, string.Empty, TimedOut: false));
        var store = new MacOsCredentialStore(runner);

        await store.GetAsync("it's-a-service", "account1", TestContext.Current.CancellationToken);

        capturedArgs.ShouldNotBeNull();
        capturedArgs.ShouldContain("it'\\''s-a-service");
    }
}
