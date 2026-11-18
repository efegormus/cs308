using System.Text.Json;
using OnlineStore.Accounts;
using OnlineStore.Seeding;

namespace OnlineStore.Tests;

public class DemoAccountCredentialsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "demo-credentials-" + Guid.NewGuid());
    private string FilePath => Path.Combine(directory, "accounts.json");

    [Fact]
    public async Task CreatesDistinctValidPasswordsForEveryDemoAccount()
    {
        var credentials = await DemoAccountCredentials.LoadAsync(FilePath, ["first@example.com", "second@example.com"]);
        Assert.Equal(2, credentials.Count); Assert.Equal(2, credentials.Values.Distinct().Count());
        Assert.All(credentials.Values, password => Assert.True(PasswordService.IsValidNewPassword(password)));
    }

    [Fact]
    public async Task RepeatedLoadPreservesAllExistingPasswordsAndFileContents()
    {
        var first = await DemoAccountCredentials.LoadAsync(FilePath, ["first@example.com"]);
        var original = await File.ReadAllTextAsync(FilePath);
        var second = await DemoAccountCredentials.LoadAsync(FilePath, ["first@example.com"]);
        Assert.Equal(first["first@example.com"], second["first@example.com"]);
        Assert.Equal(original, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task UpgradeAddsTheSecondCustomerWithoutReplacingOldCredentials()
    {
        var emails = new[] { "customer@onlinestore.invalid", "productmanager@onlinestore.invalid",
            "salesmanager@onlinestore.invalid", "supportagent@onlinestore.invalid" };
        var old = await DemoAccountCredentials.LoadAsync(FilePath, emails);
        var upgraded = await DemoAccountCredentials.LoadAsync(FilePath, emails.Append("reviewer@onlinestore.invalid"));
        Assert.Equal(5, upgraded.Count);
        foreach (var email in emails) Assert.Equal(old[email], upgraded[email]);
        Assert.True(PasswordService.IsValidNewPassword(upgraded["reviewer@onlinestore.invalid"]));
    }

    [Fact]
    public async Task CorruptCredentialFileIsNotOverwritten()
    {
        Directory.CreateDirectory(directory); await File.WriteAllTextAsync(FilePath, "not-json");
        await Assert.ThrowsAsync<JsonException>(() => DemoAccountCredentials.LoadAsync(FilePath, ["new@example.com"]));
        Assert.Equal("not-json", await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task InvalidExistingPasswordIsRejectedWithoutChangingTheFile()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(FilePath, "{\"first@example.com\":\"short\"}");
        var original = await File.ReadAllTextAsync(FilePath);
        await Assert.ThrowsAsync<InvalidDataException>(() => DemoAccountCredentials.LoadAsync(FilePath,
            ["first@example.com", "new@example.com"]));
        Assert.Equal(original, await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task CredentialFileIsPrivateOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;
        await DemoAccountCredentials.LoadAsync(FilePath, ["first@example.com"]);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
