using System.ComponentModel;
using System.Runtime.Versioning;
using Meziantou.Framework.Win32;

namespace SyntaxCircus.Credentials;

/// <summary>Stores credentials in Windows Credential Manager via the Meziantou library.</summary>
[SupportedOSPlatform("windows5.1.2600")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    public Task<string?> GetAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var targetName = $"{serviceId}/{accountId}";
        var credential = CredentialManager.ReadCredential(targetName);
        return Task.FromResult(credential?.Password);
    }

    public Task SetAsync(string serviceId, string accountId, string secret, CancellationToken ct = default)
    {
        var targetName = $"{serviceId}/{accountId}";
        CredentialManager.WriteCredential(
            applicationName: targetName,
            userName: accountId,
            secret: secret,
            persistence: CredentialPersistence.LocalMachine);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var targetName = $"{serviceId}/{accountId}";
        try
        {
            CredentialManager.DeleteCredential(targetName);
        }
        catch (Win32Exception)
        {
            // Not-found or other Credential Manager errors are silently ignored per contract.
        }

        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var value = await GetAsync(serviceId, accountId, ct).ConfigureAwait(false);
        return value is not null;
    }
}
