using Microsoft.EntityFrameworkCore;
using OnlineStore.Catalog;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Tests;

public class CatalogManagementTests
{
    private static StoreDbContext NewDb() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CatalogManagementService Service(StoreDbContext db) => new(db, TimeProvider.System);

    private static async Task<User> AddUserAsync(StoreDbContext db, UserRole role, bool active = true)
    {
        var user = new User { Name = role.ToString(), Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "hash",
            TaxId = "-", HomeAddress = "-", Role = role, IsActive = active };
        db.Users.Add(user); await db.SaveChangesAsync();
        return user;
    }

    private static ProductCreate NewProduct(long categoryId, string sku = "SKU-1", long? brandId = null) =>
        new(categoryId, brandId, sku, "Phone X", "PX-1", "SN-0001", "A phone with a good screen.", 1999.90m, 12,
            "2 years", "Acme Distribution", "acme@example.com");

    private static ProductEdit EditOf(ManagedProduct p, bool? active = true, string? sku = null, decimal? price = null) =>
        new(p.CategoryId, p.BrandId, sku ?? p.Sku, p.Name, p.Model, p.SerialNumber, p.Description, price ?? p.Price,
            p.WarrantyStatus, p.DistributorName, p.DistributorContact, active);

    // ---- validation ----

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Laptops", true)]
    public void CategoryCreate_RequiresAName(string? name, bool valid) =>
        Assert.Equal(valid, new CategoryCreate(name, null, null).Validate().Count == 0);

    [Fact]
    public void CategoryCreate_BoundsNameDescriptionAndParent()
    {
        Assert.Contains("name", new CategoryCreate(new string('x', 101), null, null).Validate().Keys);
        Assert.Contains("description", new CategoryCreate("A", new string('x', 1001), null).Validate().Keys);
        Assert.Contains("parentId", new CategoryCreate("A", null, 0).Validate().Keys);
        Assert.Empty(new CategoryCreate(new string('x', 100), new string('y', 1000), 5).Validate());
    }

    [Fact]
    public void ProductCreate_AcceptsAValidProduct() => Assert.Empty(NewProduct(1).Validate());

    [Theory]
    [InlineData("price", -0.01)]
    [InlineData("price", 10000000)]
    [InlineData("price", 1.234)]
    public void ProductCreate_RejectsABadPrice(string field, double price)
    {
        var errors = (NewProduct(1) with { Price = (decimal)price }).Validate();
        Assert.Contains(field, errors.Keys);
    }

    [Fact]
    public void ProductCreate_AllowsAZeroPriceAndZeroStock() =>
        Assert.Empty((NewProduct(1) with { Price = 0m, StockQuantity = 0 }).Validate());

    [Fact]
    public void ProductCreate_RequiresEveryField()
    {
        var empty = new ProductCreate(null, null, null, null, null, null, null, null, null, null, null, null).Validate();
        foreach (var field in new[] { "categoryId", "sku", "name", "model", "serialNumber", "description", "price",
                     "stockQuantity", "warrantyStatus", "distributorName", "distributorContact" })
            Assert.Contains(field, empty.Keys);
    }

    [Fact]
    public void ProductCreate_BoundsStockLengthsAndSkuSpaces()
    {
        Assert.Contains("stockQuantity", (NewProduct(1) with { StockQuantity = -1 }).Validate().Keys);
        Assert.Contains("stockQuantity", (NewProduct(1) with { StockQuantity = ProductRules.MaxStock + 1 }).Validate().Keys);
        Assert.Contains("sku", (NewProduct(1) with { Sku = "HAS SPACE" }).Validate().Keys);
        Assert.Contains("sku", (NewProduct(1) with { Sku = new string('x', 65) }).Validate().Keys);
        Assert.Contains("name", (NewProduct(1) with { Name = new string('x', 201) }).Validate().Keys);
        Assert.Contains("brandId", (NewProduct(1) with { BrandId = 0 }).Validate().Keys);
    }

    [Fact]
    public void ProductEdit_NeedsTheOnSaleFlag()
    {
        var edit = new ProductEdit(1, null, "S", "N", "M", "SN", "D", 1m, "W", "Dist", "c", null);
        Assert.Contains("isActive", edit.Validate().Keys);
        Assert.Empty((edit with { IsActive = false }).Validate());
    }

    [Theory]
    [InlineData("Phones", "phones")]
    [InlineData("Akıllı Telefonlar & Tablet", "akilli-telefonlar-tablet")]
    [InlineData("  TV  &  Video  ", "tv-video")]
    [InlineData("Şık Çanta Öğe", "sik-canta-oge")]
    [InlineData("!!!", "category")]
    public void Slugify_MakesAReadableAddress(string name, string expected) =>
        Assert.Equal(expected, CatalogManagementService.Slugify(name));

    // ---- roles ----

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.SalesManager)]
    [InlineData(UserRole.SupportAgent)]
    public async Task EveryOperationIsForbiddenToOtherRoles(UserRole role)
    {
        await using var db = NewDb();
        var other = await AddUserAsync(db, role);
        var service = Service(db);
        async Task Forbidden(Func<Task> action) =>
            Assert.Equal(403, (await Assert.ThrowsAsync<PurchaseException>(action)).Status);
        await Forbidden(() => service.ListCategoriesAsync(other.Id, default));
        await Forbidden(() => service.CreateCategoryAsync(other.Id, new("Phones", null, null), default));
        await Forbidden(() => service.EditCategoryAsync(other.Id, 1, new("Phones", null), default));
        await Forbidden(() => service.DeleteCategoryAsync(other.Id, 1, default));
        await Forbidden(() => service.ListProductsAsync(other.Id, 0, default));
        await Forbidden(() => service.CreateProductAsync(other.Id, NewProduct(1), default));
        await Forbidden(() => service.EditProductAsync(other.Id, 1, new(1, null, "S", "N", "M", "SN", "D", 1m, "W", "Dist", "c", true), default));
        await Forbidden(() => service.RemoveProductAsync(other.Id, 1, default));
        await Forbidden(() => service.SetStockAsync(other.Id, 1, new(5, 0), default));
        Assert.Empty(db.Categories); Assert.Empty(db.Products);
    }

    [Fact]
    public async Task AnUnknownOrDeactivatedManagerMustLogInAgain()
    {
        await using var db = NewDb();
        var inactive = await AddUserAsync(db, UserRole.ProductManager, active: false);
        var service = Service(db);
        foreach (var id in new[] { inactive.Id, inactive.Id + 99 })
            Assert.Equal(401, (await Assert.ThrowsAsync<PurchaseException>(() =>
                service.CreateCategoryAsync(id, new("Phones", null, null), default))).Status);
        Assert.Empty(db.Categories);
    }

    // ---- categories ----

    [Fact]
    public async Task CreateCategory_ReturnsItWithASlugAndCounts()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var created = await Service(db).CreateCategoryAsync(manager.Id, new(" Akıllı Saatler ", "  Watches  ", null), default);
        Assert.Equal("Akıllı Saatler", created.Name);
        Assert.Equal("akilli-saatler", created.Slug);
        Assert.Equal("Watches", created.Description);
        Assert.Equal(0, created.ProductCount); Assert.Equal(0, created.ChildCount); Assert.True(created.IsActive);
    }

    [Fact]
    public async Task CreateCategory_RejectsADuplicateNameUnderTheSameParentButAllowsAnotherParent()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var phones = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        var tablets = await service.CreateCategoryAsync(manager.Id, new("Tablets", null, null), default);
        await service.CreateCategoryAsync(manager.Id, new("Accessories", null, phones.Id), default);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.CreateCategoryAsync(manager.Id, new("Accessories", null, phones.Id), default))).Status);
        var second = await service.CreateCategoryAsync(manager.Id, new("Accessories", null, tablets.Id), default);
        // Both are called Accessories, so the second one gets a different address.
        Assert.Equal("accessories-2", second.Slug);
        Assert.Equal(1, (await service.ListCategoriesAsync(manager.Id, default)).Single(c => c.Id == phones.Id).ChildCount);
    }

    [Fact]
    public async Task CreateCategory_RejectsAnUnknownParent()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() =>
            Service(db).CreateCategoryAsync(manager.Id, new("Phones", null, 99), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() =>
            Service(db).CreateCategoryAsync(manager.Id, new("", null, null), default))).Status);
    }

    [Fact]
    public async Task EditCategory_ChangesNameAndDescriptionButKeepsTheSlug()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var phones = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        await service.CreateCategoryAsync(manager.Id, new("Tablets", null, null), default);
        var edited = await service.EditCategoryAsync(manager.Id, phones.Id, new("Smartphones", "All phones"), default);
        Assert.Equal("Smartphones", edited.Name); Assert.Equal("All phones", edited.Description);
        Assert.Equal("phones", edited.Slug);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.EditCategoryAsync(manager.Id, phones.Id, new("Tablets", null), default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() =>
            service.EditCategoryAsync(manager.Id, 99, new("X", null), default))).Status);
        // Keeping its own name is not a conflict.
        Assert.Equal("Smartphones", (await service.EditCategoryAsync(manager.Id, phones.Id, new("Smartphones", null), default)).Name);
    }

    [Fact]
    public async Task DeleteCategory_OnlyWhenEmpty()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var parent = await service.CreateCategoryAsync(manager.Id, new("Parent", null, null), default);
        var child = await service.CreateCategoryAsync(manager.Id, new("Child", null, parent.Id), default);
        await service.CreateProductAsync(manager.Id, NewProduct(child.Id), default);

        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => service.DeleteCategoryAsync(manager.Id, parent.Id, default))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => service.DeleteCategoryAsync(manager.Id, child.Id, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() => service.DeleteCategoryAsync(manager.Id, 99, default))).Status);

        var empty = await service.CreateCategoryAsync(manager.Id, new("Empty", null, null), default);
        await service.DeleteCategoryAsync(manager.Id, empty.Id, default);
        Assert.DoesNotContain(await service.ListCategoriesAsync(manager.Id, default), c => c.Id == empty.Id);
        Assert.Equal(2, (await service.ListCategoriesAsync(manager.Id, default)).Count);
    }

    // ---- products ----

    [Fact]
    public async Task CreateProduct_StoresEveryRequiredField()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var category = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        var product = await service.CreateProductAsync(manager.Id, NewProduct(category.Id, " SKU-9 "), default);
        Assert.Equal("SKU-9", product.Sku); Assert.Equal("Phone X", product.Name); Assert.Equal("PX-1", product.Model);
        Assert.Equal("SN-0001", product.SerialNumber); Assert.Equal(1999.90m, product.Price); Assert.Equal(12, product.StockQuantity);
        Assert.Equal("2 years", product.WarrantyStatus); Assert.Equal("Acme Distribution", product.DistributorName);
        Assert.Equal("acme@example.com", product.DistributorContact); Assert.Equal("Phones", product.CategoryName);
        Assert.True(product.IsActive);
        Assert.Equal(1, (await service.ListCategoriesAsync(manager.Id, default)).Single().ProductCount);
    }

    [Fact]
    public async Task CreateProduct_RejectsADuplicateSkuAnUnknownCategoryAndAnUnknownBrand()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var category = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        await service.CreateProductAsync(manager.Id, NewProduct(category.Id), default);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => service.CreateProductAsync(manager.Id, NewProduct(category.Id), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() => service.CreateProductAsync(manager.Id, NewProduct(99, "OTHER"), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() => service.CreateProductAsync(manager.Id, NewProduct(category.Id, "OTHER", brandId: 99), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() => service.CreateProductAsync(manager.Id, NewProduct(category.Id, "NEG") with { Price = -1m }, default))).Status);
        Assert.Single(db.Products);
    }

    [Fact]
    public async Task CreateProduct_SharesOneDistributorRowByName()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var category = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        await service.CreateProductAsync(manager.Id, NewProduct(category.Id, "A"), default);
        var second = await service.CreateProductAsync(manager.Id, NewProduct(category.Id, "B") with { DistributorContact = "new@example.com" }, default);
        Assert.Single(db.Distributors);
        Assert.Equal("new@example.com", second.DistributorContact);
    }

    [Fact]
    public async Task EditProduct_ChangesDetailsAndKeepsTheStockCount()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var category = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        var other = await service.CreateCategoryAsync(manager.Id, new("Tablets", null, null), default);
        var product = await service.CreateProductAsync(manager.Id, NewProduct(category.Id), default);
        var edited = await service.EditProductAsync(manager.Id, product.Id,
            EditOf(product, sku: "SKU-2", price: 25m) with { CategoryId = other.Id, Name = "Tab Y", WarrantyStatus = "1 year" }, default);
        Assert.Equal("SKU-2", edited.Sku); Assert.Equal(25m, edited.Price); Assert.Equal("Tab Y", edited.Name);
        Assert.Equal("Tablets", edited.CategoryName); Assert.Equal("1 year", edited.WarrantyStatus);
        Assert.Equal(12, edited.StockQuantity);
        // Its own SKU is not a conflict; another product's SKU is.
        await service.EditProductAsync(manager.Id, product.Id, EditOf(edited), default);
        var second = await service.CreateProductAsync(manager.Id, NewProduct(category.Id, "TAKEN"), default);
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => service.EditProductAsync(manager.Id, second.Id, EditOf(second, sku: "SKU-2"), default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() => service.EditProductAsync(manager.Id, 99, EditOf(second), default))).Status);
    }

    [Fact]
    public async Task RemoveProduct_TakesItOffSaleKeepsTheRowAndCanBePutBack()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var category = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        var product = await service.CreateProductAsync(manager.Id, NewProduct(category.Id), default);

        var removed = await service.RemoveProductAsync(manager.Id, product.Id, default);
        Assert.False(removed.IsActive);
        Assert.False((await service.RemoveProductAsync(manager.Id, product.Id, default)).IsActive);
        Assert.Single(db.Products);
        Assert.Single((await service.ListProductsAsync(manager.Id, 0, default)).Products);

        var back = await service.EditProductAsync(manager.Id, product.Id, EditOf(removed, active: true), default);
        Assert.True(back.IsActive);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() => service.RemoveProductAsync(manager.Id, 99, default))).Status);
    }

    [Fact]
    public async Task ListProducts_PagesByIdAndIncludesProductsOffSale()
    {
        await using var db = NewDb();
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var category = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        for (var i = 0; i < CatalogManagementService.PageSize + 1; i++)
            await service.CreateProductAsync(manager.Id, NewProduct(category.Id, $"P-{i}"), default);
        await service.RemoveProductAsync(manager.Id, (await service.ListProductsAsync(manager.Id, 0, default)).Products[0].Id, default);
        var first = await service.ListProductsAsync(manager.Id, 0, default);
        Assert.Equal(CatalogManagementService.PageSize, first.Products.Count);
        Assert.NotNull(first.NextAfterId);
        Assert.Contains(first.Products, p => !p.IsActive);
        var second = await service.ListProductsAsync(manager.Id, first.NextAfterId!.Value, default);
        Assert.Single(second.Products); Assert.Null(second.NextAfterId);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() => service.ListProductsAsync(manager.Id, -1, default))).Status);
    }

    // ---- stock (SCRUM-78) ----

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(25, 10, true)]
    [InlineData(1_000_000, 0, true)]
    [InlineData(-1, 0, false)]
    [InlineData(1_000_001, 0, false)]
    [InlineData(5, -1, false)]
    public void StockChange_BoundsTheNewAndTheExpectedCount(int quantity, int expected, bool valid) =>
        Assert.Equal(valid, new StockChange(quantity, expected).Validate().Count == 0);

    [Fact]
    public void StockChange_NeedsBothNumbers()
    {
        var errors = new StockChange(null, null).Validate();
        Assert.Contains("quantity", errors.Keys); Assert.Contains("expectedQuantity", errors.Keys);
        Assert.Contains("below zero", new StockChange(-3, 0).Validate()["quantity"][0]);
    }

    private static async Task<(CatalogManagementService Service, User Manager, ManagedProduct Product)> SeedStockAsync(StoreDbContext db)
    {
        var manager = await AddUserAsync(db, UserRole.ProductManager);
        var service = Service(db);
        var category = await service.CreateCategoryAsync(manager.Id, new("Phones", null, null), default);
        var product = await service.CreateProductAsync(manager.Id, NewProduct(category.Id), default);
        return (service, manager, product);
    }

    [Fact]
    public async Task SetStock_ChangesOnlyTheStockCount()
    {
        await using var db = NewDb();
        var (service, manager, product) = await SeedStockAsync(db);
        var updated = await service.SetStockAsync(manager.Id, product.Id, new(40, product.StockQuantity), default);
        Assert.Equal(40, updated.StockQuantity);
        Assert.Equal(product with { StockQuantity = 40 }, updated);
        Assert.Equal(40, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task SetStock_CanSetZeroAndRestockAProductOffSale()
    {
        await using var db = NewDb();
        var (service, manager, product) = await SeedStockAsync(db);
        await service.RemoveProductAsync(manager.Id, product.Id, default);
        Assert.Equal(0, (await service.SetStockAsync(manager.Id, product.Id, new(0, 12), default)).StockQuantity);
        var restocked = await service.SetStockAsync(manager.Id, product.Id, new(9, 0), default);
        Assert.Equal(9, restocked.StockQuantity); Assert.False(restocked.IsActive);
    }

    [Fact]
    public async Task SetStock_NeverGoesBelowZeroAndKeepsTheOldCount()
    {
        await using var db = NewDb();
        var (service, manager, product) = await SeedStockAsync(db);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() => service.SetStockAsync(manager.Id, product.Id, new(-1, 12), default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<PurchaseException>(() => service.SetStockAsync(manager.Id, product.Id, new(ProductRules.MaxStock + 1, 12), default))).Status);
        Assert.Equal(12, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task SetStock_RefusesAStaleScreenAndKeepsTheCurrentCount()
    {
        await using var db = NewDb();
        var (service, manager, product) = await SeedStockAsync(db);
        // A sale took 2 units while the manager looked at 12.
        var sold = await db.Products.SingleAsync(); sold.StockQuantity = 10; await db.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<PurchaseException>(() => service.SetStockAsync(manager.Id, product.Id, new(50, 12), default))).Status);
        Assert.Equal(10, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        Assert.Equal(50, (await service.SetStockAsync(manager.Id, product.Id, new(50, 10), default)).StockQuantity);
    }

    [Fact]
    public async Task SetStock_RepeatingTheSameChangeIsASafeRetry()
    {
        await using var db = NewDb();
        var (service, manager, product) = await SeedStockAsync(db);
        await service.SetStockAsync(manager.Id, product.Id, new(30, 12), default);
        // The answer to the first request was lost, so the screen sends it again with the old count.
        Assert.Equal(30, (await service.SetStockAsync(manager.Id, product.Id, new(30, 12), default)).StockQuantity);
        Assert.Equal(30, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task SetStock_UnknownProductIs404AndAnInvalidManagerIsRefused()
    {
        await using var db = NewDb();
        var (service, manager, product) = await SeedStockAsync(db);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() => service.SetStockAsync(manager.Id, 99, new(1, 0), default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<PurchaseException>(() => service.SetStockAsync(manager.Id, 0, new(1, 0), default))).Status);
        Assert.Equal(401, (await Assert.ThrowsAsync<PurchaseException>(() => service.SetStockAsync(manager.Id + 99, product.Id, new(1, 12), default))).Status);
    }
}
