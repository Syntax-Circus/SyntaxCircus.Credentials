namespace SyntaxCircus.Credentials;

/// <summary>Stores credentials in macOS Keychain via the <c>security</c> CLI.</summary>
public sealed class MacOsCredentialStore(IProcessRunner processRunner) : ICredentialStore
{
    public async Task<string?> GetAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var args = $"find-generic-password -s {ShellEscape(serviceId)} -a {ShellEscape(accountId)} -w";
        var result = await processRunner.RunAsync("security", args, ct).ConfigureAwait(false);

        return result.ExitCode == 0 ? result.StdOut : null;
    }

    public async Task SetAsync(string serviceId, string accountId, string secret, CancellationToken ct = default)
    {
        // -U flag updates the existing entry if present.
        var args = $"add-generic-password -U -s {ShellEscape(serviceId)} -a {ShellEscape(accountId)} -w {ShellEscape(secret)}";
        await processRunner.RunAsync("security", args, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var args = $"delete-generic-password -s {ShellEscape(serviceId)} -a {ShellEscape(accountId)}";
        // Ignore exit code — deletion of a non-existent entry is not an error per contract.
        await processRunner.RunAsync("security", args, ct).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var value = await GetAsync(serviceId, accountId, ct).ConfigureAwait(false);
        return value is not null;
    }

    /// <summary>Escapes a value for safe inclusion in a shell command argument.</summary>
    private static string ShellEscape(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
