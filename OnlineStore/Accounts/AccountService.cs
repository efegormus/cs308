using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Accounts;

public sealed class AccountService(StoreDbContext db, PasswordService passwords)
{
    public async Task<User?> CreateCustomerAsync(CustomerProfileInput profile, string password, CancellationToken ct = default)
    {
        if (CustomerProfileValidation.Validate(profile).Count != 0 || !PasswordService.IsValidNewPassword(password))
            throw new ArgumentException("A valid customer profile and password are required.");
        var normalizedEmail = profile.Email!.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(user => user.Email.ToUpper() == normalizedEmail, ct)) return null;

        var user = new User { Name = "", Email = "", PasswordHash = "", Role = UserRole.Customer };
        CustomerProfileValidation.Apply(user, profile);
        passwords.SetPassword(user, password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is MySqlException { Number: 1062 })
        {
            db.Entry(user).State = EntityState.Detached;
            return null;
        }
        return user;
    }

    public async Task<User?> AuthenticateAsync(string? email, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || password is not { Length: > 0 and <= 128 })
            return null;
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(user => user.Email.ToUpper() == normalizedEmail, ct);
        if (user is null)
        {
            passwords.VerifyMissingUser(password);
            return null;
        }
        var result = passwords.Verify(user, password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (!Enum.IsDefined(user.Role)) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded || string.IsNullOrEmpty(user.SecurityStamp))
        {
            passwords.UpgradeVerifiedPassword(user, password);
            await db.SaveChangesAsync(ct);
        }
        return user;
    }

    // Trusted operator/application service only. There is no public role-assignment endpoint.
    public async Task<bool> SetRoleAsync(string email, UserRole role, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(user => user.Email.ToUpper() == normalizedEmail, ct);
        if (user is null) return false;
        user.Role = role;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        return true;
    }
}
