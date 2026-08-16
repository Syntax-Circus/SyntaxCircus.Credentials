using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SyntaxCircus.Credentials;

/// <summary>
/// Stores credentials in an AES-256-CBC encrypted JSON file, keyed off a machine-specific value
/// (so the file isn't portable to another machine as plaintext-equivalent). Used as the Linux
/// fallback when <c>secret-tool</c> isn't on <c>PATH</c>, and available as an explicit opt-in for
/// platforms <see cref="CredentialStoreFactory"/> doesn't otherwise recognize — this is a
/// last-resort store, not a substitute for a real OS keychain.
/// </summary>
public sealed class EncryptedFileCredentialStore(string filePath) : ICredentialStore
{
    /// <summary>
    /// Computes the conventional fallback file path for an app: <c>%AppData%/{appName}/credentials.enc</c>
    /// (or the platform equivalent of <see cref="Environment.SpecialFolder.ApplicationData"/>).
    /// </summary>
    public static string DefaultPathFor(string appName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, appName, "credentials.enc");
    }

    public Task<string?> GetAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var store = ReadStore();
        return Task.FromResult(store.GetValueOrDefault($"{serviceId}/{accountId}"));
    }

    public Task SetAsync(string serviceId, string accountId, string secret, CancellationToken ct = default)
    {
        var store = ReadStore();
        store[$"{serviceId}/{accountId}"] = secret;
        WriteStore(store);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var store = ReadStore();
        store.Remove($"{serviceId}/{accountId}");
        WriteStore(store);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string serviceId, string accountId, CancellationToken ct = default)
    {
        var value = await GetAsync(serviceId, accountId, ct).ConfigureAwait(false);
        return value is not null;
    }

    private Dictionary<string, string> ReadStore()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var encrypted = File.ReadAllBytes(filePath);
            var decrypted = Decrypt(encrypted);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted) ?? [];
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException or ArgumentOutOfRangeException)
        {
            // Corrupt, foreign-key-encrypted, or unreadable file — treat as empty rather than fail.
            return [];
        }
    }

    private void WriteStore(Dictionary<string, string> store)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(store);
        var encrypted = Encrypt(json);
        File.WriteAllBytes(filePath, encrypted);
    }

    private static byte[] DeriveKey()
    {
        // Derive the encryption key from a machine-specific value + a fixed salt. This makes the
        // file non-portable as plaintext-equivalent, not cryptographically hardened against an
        // attacker with local access — the same threat model as an OS keychain on a compromised
        // account.
        string machineId;
        try
        {
            machineId = File.ReadAllText("/etc/machine-id").Trim();
        }
        catch (IOException)
        {
            machineId = Environment.MachineName;
        }

        var salt = "SyntaxCircus.Credentials-EncryptedFileFallback-Salt"u8.ToArray();
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(machineId), salt, iterations: 100_000, HashAlgorithmName.SHA256, outputLength: 32);
    }

    private static byte[] Encrypt(byte[] plaintext)
    {
        var key = DeriveKey();
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        ms.Write(iv);

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(plaintext);
        }

        return ms.ToArray();
    }

    private static byte[] Decrypt(byte[] ciphertext)
    {
        var key = DeriveKey();
        var iv = ciphertext[..16];
        var data = ciphertext[16..];

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
        {
            cs.Write(data);
        }

        return ms.ToArray();
    }
}
