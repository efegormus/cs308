using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts", table =>
        {
            table.HasCheckConstraint("CK_Carts_State", "`State` IN ('Open', 'CheckedOut', 'Abandoned')");
            // Exactly one owner: a user or a guest token, never both and never neither.
            table.HasCheckConstraint("CK_Carts_Owner",
                "(`UserId` IS NOT NULL AND `GuestTokenHash` IS NULL) OR (`UserId` IS NULL AND `GuestTokenHash` IS NOT NULL)");
        });

        builder.HasKey(cart => cart.Id);
        builder.Property(cart => cart.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(cart => cart.UserId).HasColumnType("int");
        builder.Property(cart => cart.GuestTokenHash).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(cart => cart.State)
            .HasConversion<string>()
            .HasColumnType("varchar(20)").HasMaxLength(20)
            .UseCollation("utf8mb4_bin")
            .HasDefaultValue(CartState.Open);
        builder.Property(cart => cart.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);
        builder.Property(cart => cart.UpdatedAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(cart => cart.GuestTokenHash).IsUnique();
        builder.HasIndex(cart => new { cart.UserId, cart.State });

        // A cart is a draft owned by the user, so it goes away with the user.
        builder.HasOne<User>().WithMany()
            .HasForeignKey(cart => cart.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", table =>
            table.HasCheckConstraint("CK_CartItems_Quantity", "`Quantity` > 0"));

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(item => item.CartId).HasColumnType("int");
        builder.Property(item => item.ProductId).HasColumnType("int");
        builder.Property(item => item.Quantity).HasColumnType("int");

        // One line per product; more units use Quantity.
        builder.HasIndex(item => new { item.CartId, item.ProductId }).IsUnique();
        builder.HasIndex(item => item.ProductId);

        builder.HasOne(item => item.Cart)
            .WithMany(cart => cart.Items)
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Product).WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
