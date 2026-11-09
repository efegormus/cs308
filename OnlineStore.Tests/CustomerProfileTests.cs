using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Accounts;
using OnlineStore.Data;
using OnlineStore.Models;

namespace OnlineStore.Tests;

public class CustomerProfileTests
{
    private static CustomerProfileInput Valid => new("Customer", "person@example.com", "TAX-123", "10 Example Street");

    [Fact]
    public void Profile_AcceptsRequiredFields() => Assert.Empty(CustomerProfileValidation.Validate(Valid));

    [Theory]
    [InlineData("name")]
    [InlineData("email")]
    [InlineData("taxId")]
    [InlineData("homeAddress")]
    public void Profile_RejectsMissingFields(string field)
    {
        var profile = field switch
        {
            "name" => Valid with { Name = " " },
            "email" => Valid with { Email = null },
            "taxId" => Valid with { TaxId = " " },
            _ => Valid with { HomeAddress = "" }
        };
        Assert.Contains(field, CustomerProfileValidation.Validate(profile).Keys);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Person <person@example.com>")]
    [InlineData("person@example.com\r\nBcc: other@example.com")]
    public void Profile_RejectsInvalidEmail(string email) =>
        Assert.Contains("email", CustomerProfileValidation.Validate(Valid with { Email = email }).Keys);

    [Theory]
    [InlineData("name", 101)]
    [InlineData("email", 255)]
    [InlineData("taxId", 65)]
    [InlineData("homeAddress", 1001)]
    public void Profile_RejectsOverlongFields(string field, int length)
    {
        var text = new string('a', length);
        var profile = field switch
        {
            "name" => Valid with { Name = text },
            "email" => Valid with { Email = text },
            "taxId" => Valid with { TaxId = text },
            _ => Valid with { HomeAddress = text }
        };
        Assert.Contains(field, CustomerProfileValidation.Validate(profile).Keys);
    }

    [Fact]
    public void Profile_TrimsInputWithoutChangingTaxIdFormat()
    {
        var user = new User { Name = "old", Email = "old@example.com", PasswordHash = "stored-hash" };
        CustomerProfileValidation.Apply(user, Valid with { Name = " Customer ", TaxId = " TAX-123 " });
        Assert.Equal("Customer", user.Name);
        Assert.Equal("TAX-123", user.TaxId);
        Assert.Equal("10 Example Street", user.HomeAddress);
        Assert.Equal("stored-hash", user.PasswordHash);
    }

    [Fact]
    public void Profile_InvalidInputCannotModifyUser()
    {
        var user = new User { Name = "old", Email = "old@example.com", PasswordHash = "hash" };
        Assert.Throws<ArgumentException>(() => CustomerProfileValidation.Apply(user, Valid with { TaxId = "" }));
        Assert.Equal("old", user.Name);
    }

    [Fact]
    public void Profile_ResponseExcludesPasswordHash()
    {
        var user = new User { Id = 7, Name = "Customer", Email = "person@example.com", PasswordHash = "secret-hash",
            TaxId = "TAX-123", HomeAddress = "10 Example Street" };
        var dto = CustomerProfileDto.FromUser(user);
        Assert.Equal(user.TaxId, dto.TaxId);
        Assert.Equal(user.HomeAddress, dto.HomeAddress);
        Assert.Equal(7, dto.Id);
        Assert.DoesNotContain("secret-hash", JsonSerializer.Serialize(dto));
        Assert.DoesNotContain("password", JsonSerializer.Serialize(dto), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Profile_ModelRequiresBoundedFieldsAndUniqueEmail()
    {
        using var db = new StoreDbContext(new DbContextOptionsBuilder<StoreDbContext>()
            .UseMySql("Server=127.0.0.1;Database=model_only;User ID=test", new MySqlServerVersion(new Version(8, 4, 13))).Options);
        var model = db.Model.FindEntityType(typeof(User))!;
        Assert.Equal("varbinary(256)", model.FindProperty(nameof(User.TaxId))!.GetColumnType());
        Assert.Null(model.FindProperty(nameof(User.HomeAddress)));
        Assert.Contains(model.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(User.EmailBlindIndex));
        Assert.Equal("varbinary(2048)", db.Model.FindEntityType(typeof(Address))!.FindProperty(nameof(Address.Text))!.GetColumnType());
    }
}
