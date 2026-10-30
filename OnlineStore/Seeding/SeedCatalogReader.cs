using System.Text.Json;

namespace OnlineStore.Seeding;

public static class SeedCatalogReader
{
    public static SeedCatalog ReadFromFile(string filePath)
    {
        // Load development sample data. This does NOT insert anything into MySQL.
        var json = File.ReadAllText(filePath);

        var catalog = JsonSerializer.Deserialize<SeedCatalog>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return catalog ?? throw new InvalidDataException("Seed file is empty or invalid.");
    }
}
