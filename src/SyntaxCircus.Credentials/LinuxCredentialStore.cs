namespace SyntaxCircus.Credentials;

/// <summary>
/// Stores credentials on Linux using <c>secret-tool</c> (libsecret), falling back to an
/// <see cref="EncryptedFileCredentialStore"/> when <c>secret-tool</c> isn't on <c>PATH</c>
/// (e.g. headless environments without a keyring daemon).
/// </summary>
public sealed partial class LinuxCredentialStore : ICredentialStore
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<LinuxCredentialStore> _logger;
    private readonly EncryptedFileCredentialStore _fallback;
    private bool? _secretToolAvailable;
    private bool _fallbackWarningShown;

    public LinuxCredentialStore(IProcessRunner processRunner, ILogger<LinuxCredentialStore> logger, string fallbackAppName = "SyntaxCircus")
    {
        _processRunner = processRunner;
        _logger = logger;
        _fallback = new EncryptedFileCredentialStore(EncryptedFileCredentialStore.DefaultPathFor(fallbackAppName));
    }

    public async Task<string?> GetAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        if (await IsSecretToolAvailableAsync(ct).ConfigureAwait(false))
        {
            var args = $"lookup service {serviceId} account {accountId}";
            var result = await _processRunner.RunAsync("secret-tool", args, ct).ConfigureAwait(false);
            return result.ExitCode == 0 && !string.IsNullOrEmpty(result.StdOut) ? result.StdOut : null;
        }

        return await _fallback.GetAsync(serviceId, accountId, ct).ConfigureAwait(false);
    }

    public async Task SetAsync(string serviceId, string accountId, string secret, CancellationToken ct = default)
    {
        if (await IsSecretToolAvailableAsync(ct).ConfigureAwait(false))
        {
            var args = $"store --label=\"{serviceId}/{accountId}\" service {serviceId} account {accountId}";
            await _processRunner.RunAsync("secret-tool", args, ct, stdinData: secret).ConfigureAwait(false);
            return;
        }

        await _fallback.SetAsync(serviceId, accountId, secret, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        if (await IsSecretToolAvailableAsync(ct).ConfigureAwait(false))
        {
            var args = $"clear service {serviceId} account {accountId}";
            await _processRunner.RunAsync("secret-tool", args, ct).ConfigureAwait(false);
            return;
        }

        await _fallback.DeleteAsync(serviceId, accountId, ct).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var value = await GetAsync(serviceId, accountId, ct).ConfigureAwait(false);
        return value is not null;
    }

    private async Task<bool> IsSecretToolAvailableAsync(CancellationToken ct)
    {
        if (_secretToolAvailable.HasValue)
        {
            return _secretToolAvailable.Value;
        }

        var result = await _processRunner.RunAsync("which", "secret-tool", ct, timeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        _secretToolAvailable = result.ExitCode == 0;

        if (!_secretToolAvailable.Value && !_fallbackWarningShown)
        {
            LogFallbackInUse(_logger);
            _fallbackWarningShown = true;
        }

        return _secretToolAvailable.Value;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Secure credential storage (libsecret) not available. Using encrypted file storage as fallback.")]
    private static partial void LogFallbackInUse(ILogger logger);
}
