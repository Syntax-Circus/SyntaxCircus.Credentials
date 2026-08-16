namespace SyntaxCircus.Credentials.Tests;

public class EncryptedFileCredentialStoreTests : IDisposable
{
    private readonly TempDirectory _tempDirectory = new();

    public void Dispose()
    {
        _tempDirectory.Dispose();
        GC.SuppressFinalize(this);
    }

    private string CredentialsFilePath => Path.Combine(_tempDirectory.Path, "credentials.enc");

    [Fact]
    public async Task GetAsync_FileDoesNotExist_ReturnsNull()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);

        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTripsSecret()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);

        await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);
        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBe("s3cr3t");
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingSecretForSameKey()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);

        await store.SetAsync("service1", "account1", "first", TestContext.Current.CancellationToken);
        await store.SetAsync("service1", "account1", "second", TestContext.Current.CancellationToken);
        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBe("second");
    }

    [Fact]
    public async Task DeleteAsync_RemovesSecret()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);
        await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);

        await store.DeleteAsync("service1", "account1", TestContext.Current.CancellationToken);
        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentEntry_DoesNotThrow()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);

        await Should.NotThrowAsync(() => store.DeleteAsync("service1", "account1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistsAsync_AfterSet_ReturnsTrue()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);
        await store.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);

        (await store.ExistsAsync("service1", "account1", TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NeverSet_ReturnsFalse()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);

        (await store.ExistsAsync("service1", "account1", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task MultipleServiceAccountKeys_DoNotCollide()
    {
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);

        await store.SetAsync("service1", "account1", "secret-a", TestContext.Current.CancellationToken);
        await store.SetAsync("service1", "account2", "secret-b", TestContext.Current.CancellationToken);
        await store.SetAsync("service2", "account1", "secret-c", TestContext.Current.CancellationToken);

        (await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken)).ShouldBe("secret-a");
        (await store.GetAsync("service1", "account2", TestContext.Current.CancellationToken)).ShouldBe("secret-b");
        (await store.GetAsync("service2", "account1", TestContext.Current.CancellationToken)).ShouldBe("secret-c");
    }

    [Fact]
    public async Task SeparateStoreInstance_SamePath_ReadsPersistedSecret()
    {
        var writer = new EncryptedFileCredentialStore(CredentialsFilePath);
        await writer.SetAsync("service1", "account1", "s3cr3t", TestContext.Current.CancellationToken);

        var reader = new EncryptedFileCredentialStore(CredentialsFilePath);
        var result = await reader.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBe("s3cr3t");
    }

    [Fact]
    public async Task GetAsync_CorruptFile_ReturnsNullInsteadOfThrowing()
    {
        await File.WriteAllBytesAsync(CredentialsFilePath, [1, 2, 3, 4, 5], TestContext.Current.CancellationToken);
        var store = new EncryptedFileCredentialStore(CredentialsFilePath);

        var result = await store.GetAsync("service1", "account1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public void DefaultPathFor_ValidAppName_ReturnsPathEndingInCredentialsFile()
    {
        var path = EncryptedFileCredentialStore.DefaultPathFor("MyApp");

        path.ShouldContain("MyApp");
        path.ShouldEndWith("credentials.enc");
    }

    [Fact]
    public void DefaultPathFor_NullAppName_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => EncryptedFileCredentialStore.DefaultPathFor(null!));

    [Fact]
    public void DefaultPathFor_EmptyAppName_ThrowsArgumentException()
        => Should.Throw<ArgumentException>(() => EncryptedFileCredentialStore.DefaultPathFor(string.Empty));
}
