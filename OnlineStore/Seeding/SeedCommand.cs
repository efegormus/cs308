using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OnlineStore.Data;

namespace OnlineStore.Seeding;

// Handles `dotnet run --project OnlineStore -- --seed`: loads the sample catalog and exits.
public static class SeedCommand
{
    public const string Flag = "--seed";

    public static async Task<int> RunAsync(WebApplication app)
    {
        // Sample data is for development and demos only, never for a real deployment.
        if (!app.Environment.IsDevelopment())
        {
            Console.Error.WriteLine("Seeding is only allowed in the Development environment.");
            return 1;
        }

        var filePath = Path.GetFullPath(
            Path.Combine(app.Environment.ContentRootPath, "..", "SeedData", "seed-products.json"));
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Seed file not found: {filePath}");
            return 1;
        }

        try
        {
            var catalog = SeedCatalogReader.ReadFromFile(filePath);
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();

            var result = await SeedDataLoader.LoadAsync(db, catalog);
            Console.WriteLine(
                $"Seed finished: {result.CategoriesAdded} categories and {result.ProductsAdded} products added.");
            return 0;
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            // No exception text here: it could contain connection details.
            Console.Error.WriteLine(
                "Could not write to the database. Is MySQL running and are the migrations applied?");
            return 1;
        }
    }
}
