namespace OnlineStore.Models;

public partial class Category
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
