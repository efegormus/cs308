using System.Net.Mail;
using OnlineStore.Models;

namespace OnlineStore.Accounts;

public sealed record CustomerProfileInput(string? Name, string? Email, string? TaxId, string? HomeAddress);

// This response is private to the account owner. Never serialize the User entity.
public sealed record CustomerProfileDto(int Id, string Name, string Email, string TaxId, string HomeAddress)
{
    public static CustomerProfileDto FromUser(User user) =>
        new(user.Id, user.Name, user.Email, user.TaxId, user.HomeAddress);
}

public static class CustomerProfileValidation
{
    public static Dictionary<string, string[]> Validate(CustomerProfileInput profile)
    {
        var errors = new Dictionary<string, string[]>();
        Required(profile.Name, "name", 100, errors);
        Required(profile.Email, "email", 254, errors);
        Required(profile.TaxId, "taxId", 64, errors);
        Required(profile.HomeAddress, "homeAddress", 1000, errors);

        var email = profile.Email?.Trim();
        if (!errors.ContainsKey("email") && (!MailAddress.TryCreate(email, out var address)
            || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)))
        {
            errors["email"] = ["Enter a valid email address without a display name."];
        }

        return errors;
    }

    public static void Apply(User user, CustomerProfileInput profile)
    {
        if (Validate(profile).Count != 0)
            throw new ArgumentException("A complete, valid customer profile is required.", nameof(profile));

        user.Name = profile.Name!.Trim();
        user.Email = profile.Email!.Trim();
        user.TaxId = profile.TaxId!.Trim();
        user.HomeAddress = profile.HomeAddress!.Trim();
    }

    private static void Required(string? value, string field, int maxLength, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[field] = ["This field is required."];
        else if (value.Trim().Length > maxLength)
            errors[field] = [$"Use at most {maxLength} characters."];
    }
}
