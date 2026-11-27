using Microsoft.EntityFrameworkCore;
using OnlineStore.Catalog;
using OnlineStore.Data;

namespace OnlineStore.Tests;

// The in-memory provider accepts any query shape. Real MySQL has to turn it into SQL, so these tests translate the
// catalog management queries with the MySQL provider. They open no connection and need no database.
public class CatalogManagementSqlTests
{
    private static StoreDbContext MySqlModel() => new(new DbContextOptionsBuilder<StoreDbContext>()
        .UseMySql("Server=127.0.0.1;Database=model_only;User ID=test", new MySqlServerVersion(new Version(8, 4, 13))).Options);

    [Fact]
    public void TheCategoryListTranslatesToSql()
    {
        using var db = MySqlModel();
        var sql = new CatalogManagementService(db, TimeProvider.System).CategoryListQuery().ToQueryString();
        Assert.Contains("FROM `Categories`", sql);
        Assert.Contains("ORDER BY", sql);
    }

    [Fact]
    public void TheProductListTranslatesToSql()
    {
        using var db = MySqlModel();
        var sql = new CatalogManagementService(db, TimeProvider.System).ProductPageQuery(0).ToQueryString();
        Assert.Contains("FROM `Products`", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void TheSingleRowViewsTranslateToSql()
    {
        using var db = MySqlModel();
        var service = new CatalogManagementService(db, TimeProvider.System);
        Assert.Contains("FROM `Categories`", service.CategoryViews(db.Categories.AsNoTracking().Where(c => c.Id == 1)).ToQueryString());
        Assert.Contains("FROM `Products`", service.ProductViews(db.Products.AsNoTracking().Where(p => p.Id == 1)).ToQueryString());
    }
}
