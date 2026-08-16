namespace SyntaxCircus.Credentials.Tests;

public class CredentialStoreFactoryTests
{
    [Fact]
    public void Create_NullProcessRunner_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() =>
            CredentialStoreFactory.Create(null!, NullLoggerFactory.Instance));

    [Fact]
    public void Create_NullLoggerFactory_ThrowsArgumentNullException()
        => Should.Throw<ArgumentNullException>(() =>
            CredentialStoreFactory.Create(Substitute.For<IProcessRunner>(), null!));

    // The CI/dev runner for this repo is Windows, so only the Windows branch is exercisable here.
    // macOS/Linux/unsupported-platform branches are simple `if (OperatingSystem.Is...)` checks with
    // no seam to fake the OS — covered by inspection, not by an automated test, on this platform.
    [Fact]
    public void Create_OnWindows_ReturnsWindowsCredentialStore()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
        {
            return;
        }

        var store = CredentialStoreFactory.Create(Substitute.For<IProcessRunner>(), NullLoggerFactory.Instance);

        store.ShouldBeOfType<WindowsCredentialStore>();
    }
}
