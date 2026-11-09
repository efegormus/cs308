using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

public class PersonalDataProtectionTests
{
    [Fact]
    public void Encryption_UsesFreshNoncesAndAuthenticatesCiphertext()
    {
        var protector = new PersonalDataProtector(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
        const string value = "İstanbul, müşteri@example.com";
        var first = protector.Encrypt(value);
        var second = protector.Encrypt(value);
        Assert.False(first.SequenceEqual(second));
        Assert.Equal(value, protector.Decrypt(first));
        Assert.Equal(Encoding.UTF8.GetByteCount(value) + 28, first.Length);
        first[15] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => protector.Decrypt(first));
    }

    [Fact]
    public void EmailBlindIndex_NormalizesCaseAndWhitespaceWithoutRevealingEmail()
    {
        var protector = new PersonalDataProtector(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
        Assert.Equal(protector.EmailIndex(" Buyer@example.com "), protector.EmailIndex("buyer@EXAMPLE.COM"));
        Assert.NotEqual(protector.EmailIndex("other@example.com"), protector.EmailIndex("buyer@example.com"));
        Assert.Equal(32, protector.EmailIndex("buyer@example.com").Length);
    }
}

[Collection("MySql")]
public class RedesignMySqlTests(MySqlFixture fixture) : IClassFixture<MySqlFixture>
{
    private async Task<(long Buyer, long Other, long Product, long Line)> PurchaseAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.Create();
        var buyer = new User { Name = "Buyer", Email = "buyer@example.com", TaxId = "DEMO-TAX", HomeAddress = "İstanbul demo address", PasswordHash = "test-only" };
        var other = new User { Name = "Other", Email = "other@example.com", TaxId = "OTHER", HomeAddress = "Other address", PasswordHash = "test-only" };
        var product = new Product { Sku = "REDESIGN", Name = "Çalışma ışığı X1", Description = "Şarj cihazı", Price = 100,
            StockQuantity = 5, Category = new Category { Name = "Lights", Slug = "lights" } };
        CatalogFixture.CompleteDetails(product);
        db.Users.AddRange(buyer, other); db.Products.Add(product);
        await db.SaveChangesAsync();
        var cart = new Cart { UserId = buyer.Id, Items = [new CartItem { ProductId = product.Id, Quantity = 1 }] };
        db.Carts.Add(cart); await db.SaveChangesAsync();
        var checkout = new CheckoutService(db, new MockPaymentGateway(TimeProvider.System), TimeProvider.System);
        var quote = await checkout.QuoteAsync(buyer.Id, default);
        await checkout.CompleteAsync(buyer.Id, new(quote.CartId, Guid.NewGuid().ToString("D"), quote.QuoteToken,
            new MockCard { Number = "4242424242424242", SecurityCode = "123", ExpiryMonth = 12, ExpiryYear = DateTime.UtcNow.Year + 1 }), default);
        return (buyer.Id, other.Id, product.Id, await db.OrderItems.Select(i => i.Id).SingleAsync());
    }

    [MySqlFact]
    public async Task PhysicalSchema_Has29DomainTablesAndEveryMappedColumn()
    {
        await using var db = fixture.Create();
        await db.Database.OpenConnectionAsync();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME NOT IN ('__EFMigrationsHistory','vw_DeliveryList')";
        var columns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                var table = reader.GetString(0);
                if (!columns.TryGetValue(table, out var names)) columns[table] = names = new(StringComparer.OrdinalIgnoreCase);
                names.Add(reader.GetString(1));
            }
        Assert.Equal(29, columns.Count);
        foreach (var entity in db.Model.GetEntityTypes())
            Assert.Equal(columns[entity.GetTableName()!].Order(), entity.GetProperties().Select(p => p.GetColumnName()!).Order());
        command.CommandText = "SELECT COUNT(*) FROM information_schema.VIEWS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='vw_DeliveryList'";
        Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [MySqlFact]
    public async Task PersonalData_IsEncryptedOnDiskAndRoundTripsThroughEf()
    {
        var g = await PurchaseAsync();
        await using var db = fixture.Create();
        await db.Database.OpenConnectionAsync();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EmailEnc,EmailBlindIndex FROM Users WHERE Id=@id";
        var id = command.CreateParameter(); id.ParameterName = "@id"; id.Value = g.Buyer; command.Parameters.Add(id);
        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            var ciphertext = (byte[])reader.GetValue(0);
            Assert.DoesNotContain("buyer@example.com", Encoding.UTF8.GetString(ciphertext));
            Assert.Equal("buyer@example.com", db.Protector.Decrypt(ciphertext));
            Assert.Equal(db.Protector.EmailIndex("BUYER@example.com"), (byte[])reader.GetValue(1));
        }
        var user = await db.Users.SingleAsync(u => u.Id == g.Buyer);
        Assert.Equal("İstanbul demo address", user.HomeAddress);
        Assert.Equal("DEMO-TAX", user.TaxId);
    }

    [MySqlFact]
    public async Task CompositeReviewForeignKey_RejectsAnotherBuyersOrderLine()
    {
        var g = await PurchaseAsync();
        await using var db = fixture.Create();
        var error = await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Ratings(UserId,ProductId,OrderItemId,Stars) VALUES({g.Other},{g.Product},{g.Line},5)"));
        Assert.Equal(1452, error.Number);
    }

    [MySqlFact]
    public async Task CountersAndDeliveryView_StayConsistentWithPurchase()
    {
        var g = await PurchaseAsync();
        await using var db = fixture.Create();
        var error = await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE OrderItems SET DeliveredQuantity=2 WHERE Id={g.Line}"));
        Assert.Equal(3819, error.Number);
        var line = await db.OrderItems.SingleAsync();
        Assert.Equal(1, line.DeliveryAllocatedQuantity);
        Assert.Equal(0, line.DeliveredQuantity);
        await db.Database.OpenConnectionAsync();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CustomerId,ProductId,Quantity,TotalPrice,IsCompleted FROM vw_DeliveryList";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal((ulong)g.Buyer, reader.GetFieldValue<ulong>(0));
        Assert.Equal((ulong)g.Product, reader.GetFieldValue<ulong>(1));
        Assert.Equal(100m, reader.GetDecimal(3));
        Assert.False(reader.GetBoolean(4));
    }

    [MySqlFact]
    public async Task ConcurrentOpenCarts_AllowOnlyOneWinner()
    {
        var g = await PurchaseAsync(); // This cart is CheckedOut, so a new open cart is allowed.
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = fixture.Create();
            db.Carts.Add(new Cart { UserId = g.Buyer });
            try { await db.SaveChangesAsync(); return true; }
            catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 }) { return false; }
        }));
        Assert.Single(results.Where(result => result));
        await using var check = fixture.Create();
        Assert.Equal(1, await check.Carts.CountAsync(c => c.UserId == g.Buyer && c.State == CartState.Open));
    }

    [MySqlFact]
    public async Task TypedSpecs_RejectMixedValuesAndWrongDeclaredType()
    {
        var g = await PurchaseAsync();
        await using var db = fixture.Create();
        var category = await db.Categories.SingleAsync();
        var definition = new AttributeDefinition { CategoryId = category.Id, Code = "power", Label = "Power", DataType = "Number", Unit = "W" };
        db.AttributeDefinitions.Add(definition); await db.SaveChangesAsync();
        var mixed = await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO ProductAttributeValues(ProductId,AttributeDefinitionId,DataType,ValueText,ValueNumber) VALUES({g.Product},{definition.Id},'Number','wrong',10)"));
        Assert.Equal(3819, mixed.Number);
        var wrongType = await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO ProductAttributeValues(ProductId,AttributeDefinitionId,DataType,ValueText) VALUES({g.Product},{definition.Id},'Text','wrong')"));
        Assert.Equal(1452, wrongType.Number);
    }
}
