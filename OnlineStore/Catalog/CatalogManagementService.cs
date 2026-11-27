using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;
using OnlineStore.Models;
using OnlineStore.Purchasing;

namespace OnlineStore.Catalog;

// SCRUM-77: the product manager creates, edits and removes products and categories.
// "Remove" keeps history safe. A product that was ever sold stays in old orders, so deleting a product takes it off
// sale (IsActive = false) and an edit can put it back. A category is only deleted when it is empty.
public sealed class CatalogManagementService(StoreDbContext db, TimeProvider clock)
{
    public const int PageSize = 50;
    private const int MaxCategories = 500;

    // ---- categories ----

    public async Task<IReadOnlyList<ManagedCategory>> ListCategoriesAsync(long managerId, CancellationToken ct)
    {
        await RequireProductManagerAsync(managerId, ct);
        return await CategoryViews(db.Categories.AsNoTracking()).OrderBy(c => c.Id).Take(MaxCategories).ToListAsync(ct);
    }

    public async Task<ManagedCategory> CreateCategoryAsync(long managerId, CategoryCreate input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Use a valid category.");
        await RequireProductManagerAsync(managerId, ct);
        if (input.ParentId is { } parentId && !await db.Categories.AnyAsync(c => c.Id == parentId, ct))
            throw new PurchaseException(400, "The parent category does not exist.");
        var name = input.Name!.Trim();
        if (await db.Categories.AnyAsync(c => c.ParentId == input.ParentId && c.Name == name, ct))
            throw new PurchaseException(409, "A category with this name already exists here.");
        var category = new Category
        {
            Name = name, ParentId = input.ParentId, Description = Clean(input.Description),
            Slug = await UniqueSlugAsync(name, ct), CreatedAt = Now()
        };
        db.Categories.Add(category);
        await SaveAsync("A category with this name already exists here.", ct);
        return await CategoryViews(db.Categories.AsNoTracking().Where(c => c.Id == category.Id)).SingleAsync(ct);
    }

    public async Task<ManagedCategory> EditCategoryAsync(long managerId, long id, CategoryEdit input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Use a valid category.");
        await RequireProductManagerAsync(managerId, ct);
        var category = id <= 0 ? null : await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) throw new PurchaseException(404, "Category not found.");
        var name = input.Name!.Trim();
        if (await db.Categories.AnyAsync(c => c.Id != id && c.ParentId == category.ParentId && c.Name == name, ct))
            throw new PurchaseException(409, "A category with this name already exists here.");
        // The slug is part of public links, so it stays the same when only the name changes.
        category.Name = name;
        category.Description = Clean(input.Description);
        await SaveAsync("A category with this name already exists here.", ct);
        return await CategoryViews(db.Categories.AsNoTracking().Where(c => c.Id == id)).SingleAsync(ct);
    }

    public async Task DeleteCategoryAsync(long managerId, long id, CancellationToken ct)
    {
        await RequireProductManagerAsync(managerId, ct);
        var category = id <= 0 ? null : await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) throw new PurchaseException(404, "Category not found.");
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            throw new PurchaseException(409, "This category still has products. Move or remove them first.");
        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct))
            throw new PurchaseException(409, "This category still has subcategories. Remove them first.");
        db.Categories.Remove(category);
        await SaveAsync("This category is still in use and cannot be deleted.", ct);
    }

    // ---- products ----

    public async Task<ManagedProductPage> ListProductsAsync(long managerId, long afterId, CancellationToken ct)
    {
        await RequireProductManagerAsync(managerId, ct);
        if (afterId < 0) throw new PurchaseException(400, "Use a non-negative product cursor.");
        var rows = await ProductViews(db.Products.AsNoTracking().Where(p => p.Id > afterId))
            .OrderBy(p => p.Id).Take(PageSize + 1).ToListAsync(ct);
        return new(rows.Take(PageSize).ToList(), rows.Count > PageSize ? rows[PageSize - 1].Id : null);
    }

    public async Task<ManagedProduct> CreateProductAsync(long managerId, ProductCreate input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Use valid product details.");
        await RequireProductManagerAsync(managerId, ct);
        await RequireCategoryAndBrandAsync(input.CategoryId!.Value, input.BrandId, ct);
        var sku = input.Sku!.Trim();
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct)) throw new PurchaseException(409, "This SKU is already used.");
        var now = Now();
        var product = new Product
        {
            Sku = sku, Name = input.Name!.Trim(), CategoryId = input.CategoryId!.Value, BrandId = input.BrandId,
            Model = input.Model!.Trim(), SerialNumber = input.SerialNumber!.Trim(), Description = input.Description!.Trim(),
            Price = input.Price!.Value, StockQuantity = input.StockQuantity!.Value,
            WarrantyStatus = input.WarrantyStatus!.Trim(), IsActive = true, CreatedAt = now, UpdatedAt = now, RowVersion = now,
            Distributor = await DistributorAsync(input.DistributorName!, input.DistributorContact!, ct)
        };
        db.Products.Add(product);
        await SaveAsync("This SKU is already used.", ct);
        return await ProductViews(db.Products.AsNoTracking().Where(p => p.Id == product.Id)).SingleAsync(ct);
    }

    public async Task<ManagedProduct> EditProductAsync(long managerId, long id, ProductEdit input, CancellationToken ct)
    {
        if (input.Validate().Count != 0) throw new PurchaseException(400, "Use valid product details.");
        await RequireProductManagerAsync(managerId, ct);
        var product = id <= 0 ? null : await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) throw new PurchaseException(404, "Product not found.");
        await RequireCategoryAndBrandAsync(input.CategoryId!.Value, input.BrandId, ct);
        var sku = input.Sku!.Trim();
        if (await db.Products.AnyAsync(p => p.Id != id && p.Sku == sku, ct)) throw new PurchaseException(409, "This SKU is already used.");
        var now = Now();
        // The stock count is not edited here (SCRUM-78). Past orders keep their own name and price snapshots.
        product.Sku = sku; product.Name = input.Name!.Trim(); product.CategoryId = input.CategoryId!.Value;
        product.BrandId = input.BrandId; product.Model = input.Model!.Trim(); product.SerialNumber = input.SerialNumber!.Trim();
        product.Description = input.Description!.Trim(); product.Price = input.Price!.Value;
        product.WarrantyStatus = input.WarrantyStatus!.Trim(); product.IsActive = input.IsActive!.Value;
        product.Distributor = await DistributorAsync(input.DistributorName!, input.DistributorContact!, ct);
        product.UpdatedAt = now; product.RowVersion = now;
        await SaveAsync("This SKU is already used.", ct);
        return await ProductViews(db.Products.AsNoTracking().Where(p => p.Id == id)).SingleAsync(ct);
    }

    // Takes the product off sale. Repeating it is safe and changes nothing.
    public async Task<ManagedProduct> RemoveProductAsync(long managerId, long id, CancellationToken ct)
    {
        await RequireProductManagerAsync(managerId, ct);
        var product = id <= 0 ? null : await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) throw new PurchaseException(404, "Product not found.");
        if (product.IsActive)
        {
            var now = Now();
            product.IsActive = false; product.UpdatedAt = now; product.RowVersion = now;
            await db.SaveChangesAsync(ct);
        }
        return await ProductViews(db.Products.AsNoTracking().Where(p => p.Id == id)).SingleAsync(ct);
    }

    // ---- helpers ----

    private async Task RequireCategoryAndBrandAsync(long categoryId, long? brandId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct)) throw new PurchaseException(400, "The category does not exist.");
        if (brandId is { } id && !await db.Brands.AnyAsync(b => b.Id == id && b.IsActive, ct))
            throw new PurchaseException(400, "The brand does not exist.");
    }

    // A distributor is found by name, so every product of one distributor shares one row. Its contact text is the
    // latest one the manager entered.
    private async Task<Distributor> DistributorAsync(string name, string contact, CancellationToken ct)
    {
        name = name.Trim(); contact = contact.Trim();
        var distributor = await db.Distributors.SingleOrDefaultAsync(d => d.Name == name, ct);
        if (distributor is null) return new Distributor { Name = name, ContactInfo = contact };
        distributor.ContactInfo = contact;
        return distributor;
    }

    private IQueryable<ManagedCategory> CategoryViews(IQueryable<Category> categories) => categories.Select(c =>
        new ManagedCategory(c.Id, c.ParentId, c.Name, c.Slug, c.Description, c.IsActive,
            db.Products.Count(p => p.CategoryId == c.Id), db.Categories.Count(child => child.ParentId == c.Id)));

    private IQueryable<ManagedProduct> ProductViews(IQueryable<Product> products) => products.Select(p =>
        new ManagedProduct(p.Id, p.CategoryId, p.Category.Name, p.BrandId, p.Sku, p.Name, p.Model, p.SerialNumber,
            p.Description ?? "", p.Price, p.StockQuantity, p.WarrantyStatus, p.Distributor.Name, p.Distributor.ContactInfo,
            p.IsActive));

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        for (var n = 2; await db.Categories.AnyAsync(c => c.Slug == slug, ct); n++)
        {
            var suffix = "-" + n.ToString(CultureInfo.InvariantCulture);
            slug = baseSlug[..Math.Min(baseSlug.Length, 120 - suffix.Length)] + suffix;
        }
        return slug;
    }

    // "Akıllı Telefonlar & Tablet" becomes "akilli-telefonlar-tablet". The Turkish letters map to plain ones.
    public static string Slugify(string name)
    {
        var map = name.Trim().ToLowerInvariant().Replace('ı', 'i').Replace('ğ', 'g').Replace('ş', 's')
            .Replace('ç', 'c').Replace('ö', 'o').Replace('ü', 'u').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var dash = false;
        foreach (var ch in map)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') { builder.Append(ch); dash = false; }
            else if (!dash && builder.Length > 0) { builder.Append('-'); dash = true; }
        }
        var slug = builder.ToString().Trim('-');
        if (slug.Length > 100) slug = slug[..100].Trim('-');
        return slug.Length == 0 ? "category" : slug;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateTime Now() { var now = clock.GetUtcNow().UtcDateTime; return now.AddTicks(-(now.Ticks % 10)); }

    // Two managers saving the same name or SKU at once: the unique key in the database decides, the loser gets 409.
    private async Task SaveAsync(string conflictMessage, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new PurchaseException(409, conflictMessage); }
    }

    private async Task RequireProductManagerAsync(long userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !user.IsActive) throw new PurchaseException(401, "Please log in again.");
        if (user.Role != UserRole.ProductManager) throw new PurchaseException(403, "A product manager account is required.");
    }
}
