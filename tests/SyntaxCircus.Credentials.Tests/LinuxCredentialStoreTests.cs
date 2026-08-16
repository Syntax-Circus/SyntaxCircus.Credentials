namespace SyntaxCircus.Credentials.Tests;

public class LinuxCredentialStoreTests
{
    private static IProcessRunner CreateProcessRunner(bool secretToolAvailable, Func<string, string, ProcessResult>? responder = null)
    {
        var runner = Substitute.For<IProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns(callInfo =>
            {
                var executable = callInfo.ArgAt<string>(0);
                var arguments = callInfo.ArgAt<string>(1);
                if (executable == "which")
                {
                    return new ProcessResult(secretToolAvailable ? 0 : 1, string.Empty, string.Empty, TimedOut: false);
                }

                return responder?.Invoke(executable, arguments) ?? new ProcessResult(0, string.Empty, string.Empty, TimedOut: false);
            });
        return runner;
    }

    [Fact]
    public async Task GetAsync_SecretToolAvailable_UsesSecretToolLookup()
    {
        var runner = CreateProcessRunner(secretToolAvailable: true, (_, _) => new ProcessResult(0, "s3cr3t", string.Empty, TimedOut: false));
        var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance);

        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBe("s3cr3t");
        await runner.Received(1).RunAsync(
            "secret-tool",
            Arg.Is<string>(args => args.Contains("lookup service service1 account account1", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task GetAsync_SecretToolLookupFails_ReturnsNull()
    {
        var runner = CreateProcessRunner(secretToolAvailable: true, (_, _) => new ProcessResult(1, string.Empty, string.Empty, TimedOut: false));
        var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance);

        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_SecretToolAvailable_PipesSecretToStdin()
    {
        string? capturedStdin = null;
        var runner = Substitute.For<IProcessRunner>();
        runner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<string?>())
            .Returns(callInfo =>
            {
                var executable = callInfo.ArgAt<string>(0);
                if (executable == "which")
                {
                    return new ProcessResult(0, string.Empty, string.Empty, TimedOut: false);
                }

                capturedStdin = callInfo.ArgAt<string?>(4);
                return new ProcessResult(0, string.Empty, string.Empty, TimedOut: false);
            });
        var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance);

        await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);

        capturedStdin.ShouldBe("s3cr3t");
        await runner.Received(1).RunAsync(
            "secret-tool",
            Arg.Is<string>(args => args.Contains("store --label=", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task DeleteAsync_SecretToolAvailable_UsesClearCommand()
    {
        var runner = CreateProcessRunner(secretToolAvailable: true);
        var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance);

        await store.DeleteAsync("service1", "account1", TestContext.Current.CancellationToken);

        await runner.Received(1).RunAsync(
            "secret-tool",
            Arg.Is<string>(args => args.Contains("clear service service1 account account1", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task ExistsAsync_SecretToolAvailableAndFound_ReturnsTrue()
    {
        var runner = CreateProcessRunner(secretToolAvailable: true, (_, _) => new ProcessResult(0, "s3cr3t", string.Empty, TimedOut: false));
        var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance);

        (await store.ExistsAsync("service1", "account1", TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task IsSecretToolAvailable_CachedAcrossMultipleCalls()
    {
        var runner = CreateProcessRunner(secretToolAvailable: true, (_, _) => new ProcessResult(0, "s3cr3t", string.Empty, TimedOut: false));
        var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance);

        await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);
        await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);
        await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);

        await runner.Received(1).RunAsync("which", Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task GetAsync_SecretToolUnavailable_FallsBackToEncryptedFileStore()
    {
        var appName = "sc-credentials-tests-linux-fallback-" + Guid.NewGuid().ToString("N");
        var fallbackDirectory = Path.GetDirectoryName(EncryptedFileCredentialStore.DefaultPathFor(appName))!;
        try
        {
            var runner = CreateProcessRunner(secretToolAvailable: false);
            var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance, appName);

            await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);
            var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

            result.ShouldBe("s3cr3t");
        }
        finally
        {
            if (Directory.Exists(fallbackDirectory))
            {
                Directory.Delete(fallbackDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeleteAsync_SecretToolUnavailable_FallsBackToEncryptedFileStore()
    {
        var appName = "sc-credentials-tests-linux-fallback-" + Guid.NewGuid().ToString("N");
        var fallbackDirectory = Path.GetDirectoryName(EncryptedFileCredentialStore.DefaultPathFor(appName))!;
        try
        {
            var runner = CreateProcessRunner(secretToolAvailable: false);
            var store = new LinuxCredentialStore(runner, NullLogger<LinuxCredentialStore>.Instance, appName);
            await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);

            await store.DeleteAsync("service1", "account1", TestContext.Current.CancellationToken);
            var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

            result.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(fallbackDirectory))
            {
                Directory.Delete(fallbackDirectory, recursive: true);
            }
        }
    }
}
