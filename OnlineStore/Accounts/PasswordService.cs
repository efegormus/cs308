using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OnlineStore.Models;

namespace OnlineStore.Accounts;

public sealed class PasswordService
{
    private readonly PasswordHasher<User> hasher;
    private readonly User dummy = new() { Name = "", Email = "", PasswordHash = "" };
    private readonly string dummyHash;

    public PasswordService(IOptions<PasswordHasherOptions> options)
    {
        hasher = new PasswordHasher<User>(options);
        dummyHash = hasher.HashPassword(dummy, Guid.NewGuid().ToString("N"));
    }

    public static bool IsValidNewPassword(string? password) => password is { Length: >= 12 and <= 128 };

    public void SetPassword(User user, string password)
    {
        if (!IsValidNewPassword(password))
            throw new ArgumentException("Use a password between 12 and 128 characters.", nameof(password));
        UpgradeVerifiedPassword(user, password);
    }

    internal void UpgradeVerifiedPassword(User user, string password)
    {
        user.PasswordHash = hasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
    }

    public PasswordVerificationResult Verify(User user, string password)
    {
        try { return hasher.VerifyHashedPassword(user, user.PasswordHash, password); }
        catch (FormatException) { return PasswordVerificationResult.Failed; }
    }

    public void VerifyMissingUser(string password) => hasher.VerifyHashedPassword(dummy, dummyHash, password);
}
