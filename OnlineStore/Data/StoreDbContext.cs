using Microsoft.EntityFrameworkCore;
using OnlineStore.Data.Configurations;
using OnlineStore.Models;
using System.ComponentModel.DataAnnotations;

namespace OnlineStore.Data;

public class StoreDbContext : DbContext
{
    public StoreDbContext(DbContextOptions<StoreDbContext> options) : base(options)
    {
    }

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
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Comment> Comments => Set<Comment>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateActiveProducts();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateActiveProducts();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
        base.OnModelCreating(modelBuilder);

        modelBuilder.UseCollation("utf8mb4_0900_ai_ci");
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new DiscountConfiguration());
        modelBuilder.ApplyConfiguration(new DiscountProductConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new CartConfiguration());
        modelBuilder.ApplyConfiguration(new CartItemConfiguration());
        modelBuilder.ApplyConfiguration(new WishlistItemConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryItemConfiguration());
        modelBuilder.ApplyConfiguration(new RatingConfiguration());
        modelBuilder.ApplyConfiguration(new CommentConfiguration());
    }
}
