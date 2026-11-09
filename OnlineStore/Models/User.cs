namespace OnlineStore.Models;

public partial class User
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string TaxId { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string HomeAddress
    {
        get => Addresses.FirstOrDefault(a => a.IsDefault && a.IsActive)?.Text ?? "";
        set
        {
            var home = Addresses.FirstOrDefault(a => a.IsDefault && a.IsActive);
            if (home is null) Addresses.Add(new Address { Label = "Home", Text = value, IsDefault = true, IsActive = true });
            else home.Text = value;
        }
    }
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
