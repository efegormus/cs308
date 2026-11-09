using Microsoft.EntityFrameworkCore;

using OnlineStore.Models;
using System.ComponentModel.DataAnnotations;

namespace OnlineStore.Data;

public class StoreDbContext : DbContext
{
    public PersonalDataProtector Protector { get; }
    public StoreDbContext(DbContextOptions<StoreDbContext> options) : base(options)
    {
        Protector = PersonalDataProtector.Load(options.Extensions.Any(e => e.GetType().Name.Contains("InMemory")));
    }

    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Distributor> Distributors => Set<Distributor>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<DiscountProduct> DiscountProducts => Set<DiscountProduct>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<InvoiceEmailDelivery> InvoiceEmailDeliveries => Set<InvoiceEmailDelivery>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreparePersonalData();
        ValidateActiveProducts();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreparePersonalData();
        ValidateActiveProducts();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PreparePersonalData()
    {
        foreach (var e in ChangeTracker.Entries<User>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            e.Entity.EmailBlindIndex = Protector.EmailIndex(e.Entity.Email);
    }

    private void ValidateActiveProducts()
    {
        foreach (var entry in ChangeTracker.Entries<Product>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var errors = ProductCatalogValidation.Validate(entry.Entity);
            if (errors.Count != 0)
                throw new ValidationException("Invalid active product fields: " + string.Join(", ", errors.Keys));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        RedesignConfiguration.Configure(modelBuilder, Protector);
    }
}
