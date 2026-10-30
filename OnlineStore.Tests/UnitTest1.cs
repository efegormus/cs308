
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace OnlineStore.Tests;

public class SeedDataTests
{
    [Fact]
    public void SeedData_ShouldContainTenProducts()
    {
        // Find the main cs308 project folder
        var projectPath = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..")
        );

        // Find our JSON file
        var filePath = Path.Combine(
            projectPath,
            "SeedData",
            "seed-products.json"
        );

        // Check that the file exists
        Assert.True(File.Exists(filePath));

        // Read and parse the JSON file
        var json = File.ReadAllText(filePath);
        using var document = JsonDocument.Parse(json);

        // Get the products array
        var products = document.RootElement
            .GetProperty("products");

        // Check that there are exactly 10 products
        Assert.Equal(10, products.GetArrayLength());
    }
}
