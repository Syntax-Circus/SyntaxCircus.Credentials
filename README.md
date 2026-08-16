# SyntaxCircus.Credentials

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Credentials/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Credentials/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Credentials.svg)](https://www.nuget.org/packages/SyntaxCircus.Credentials)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

A cross-platform desktop credential vault: Windows Credential Manager, macOS Keychain, Linux `secret-tool`/libsecret (with an AES-256 encrypted-file fallback when libsecret isn't available), and a factory that picks the right one for the current OS.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Setup

```csharp
ICredentialStore store = CredentialStoreFactory.Create(
    new ProcessRunner(),
    loggerFactory,
    allowEncryptedFileFallback: false, // opt-in only — see below
    appName: "MyApp");

await store.SetAsync("my-service", "my-account", "secret-value");
string? secret = await store.GetAsync("my-service", "my-account");
```

Credentials are keyed by a `(serviceId, accountId)` pair, matching how OS keychains natively model credentials — `serviceId` is typically your app or the external service the secret belongs to, `accountId` the specific credential within it.

## Platform behavior

- **Windows** — [Windows Credential Manager](https://learn.microsoft.com/windows/win32/secauthn/credentials-management) via `Meziantou.Framework.Win32.CredentialManager`. Works from a plain `net10.0` TFM — no `net10.0-windows` multi-targeting required.
- **macOS** — Keychain via the `security` CLI, shelled out through an injectable `IProcessRunner` (testable without spawning real processes).
- **Linux** — `secret-tool`/libsecret via the same `IProcessRunner`, if it's on `PATH`. Falls back to `EncryptedFileCredentialStore` automatically (with a one-time warning logged) on headless systems without a keyring daemon.
- **Anything else** — `CredentialStoreFactory.Create` throws `PlatformNotSupportedException` by default. Pass `allowEncryptedFileFallback: true` to degrade to `EncryptedFileCredentialStore` instead — off by default because silently falling back to a weaker store is a decision your app should make explicitly, not one this package makes for you.

## Encrypted-file fallback

`EncryptedFileCredentialStore` (AES-256-CBC, PBKDF2 key derivation off a machine-specific value + fixed salt) is usable directly too, not just as the Linux/unrecognized-platform fallback:

```csharp
var store = new EncryptedFileCredentialStore(EncryptedFileCredentialStore.DefaultPathFor("MyApp"));
```

This is a last-resort store, not a substitute for a real OS keychain — the key derivation makes the file non-portable as plaintext-equivalent, not cryptographically hardened against an attacker with local account access.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
