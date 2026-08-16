namespace SyntaxCircus.Credentials;

/// <summary>Creates the platform-appropriate <see cref="ICredentialStore"/> implementation.</summary>
public static class CredentialStoreFactory
{
    /// <summary>
    /// Resolves Windows Credential Manager, macOS Keychain, or Linux secret-tool/libsecret
    /// (falling back to an encrypted file if libsecret isn't available) based on the current OS.
    /// </summary>
    /// <param name="allowEncryptedFileFallback">
    /// When <see langword="true"/>, a genuinely unrecognized platform gets an
    /// <see cref="EncryptedFileCredentialStore"/> instead of an exception. Off by default —
    /// degrading silently to a weaker store is a decision the caller should opt into explicitly.
    /// </param>
    /// <param name="appName">
    /// Used to scope the encrypted-file fallback path (Linux without libsecret, or an
    /// unrecognized platform with <paramref name="allowEncryptedFileFallback"/> set). Defaults to
    /// <c>"SyntaxCircus"</c> — pass your own app name to avoid colliding with other apps using
    /// this package on the same machine.
    /// </param>
    public static ICredentialStore Create(
        IProcessRunner processRunner,
        ILoggerFactory loggerFactory,
        bool allowEncryptedFileFallback = false,
        string appName = "SyntaxCircus")
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
        {
            return new WindowsCredentialStore();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsCredentialStore(processRunner);
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxCredentialStore(processRunner, loggerFactory.CreateLogger<LinuxCredentialStore>(), appName);
        }

        if (allowEncryptedFileFallback)
        {
            return new EncryptedFileCredentialStore(EncryptedFileCredentialStore.DefaultPathFor(appName));
        }

        throw new PlatformNotSupportedException(
            "Unsupported operating system for credential storage. Pass allowEncryptedFileFallback: true to degrade to an encrypted file instead.");
    }
}
