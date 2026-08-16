namespace SyntaxCircus.Credentials;

/// <summary>
/// Platform-agnostic interface for securely storing and retrieving secrets, keyed by a
/// service/account pair the same way OS keychains natively model credentials.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Retrieves a stored secret, or <see langword="null"/> if not found.</summary>
    Task<string?> GetAsync(string serviceId, string accountId, CancellationToken ct = default);

    /// <summary>Stores or overwrites a secret.</summary>
    Task SetAsync(string serviceId, string accountId, string secret, CancellationToken ct = default);

    /// <summary>Removes a stored secret. Does not throw if the secret does not exist.</summary>
    Task DeleteAsync(string serviceId, string accountId, CancellationToken ct = default);

    /// <summary>Returns <see langword="true"/> if a secret exists for the given identifiers.</summary>
    Task<bool> ExistsAsync(string serviceId, string accountId, CancellationToken ct = default);
}
