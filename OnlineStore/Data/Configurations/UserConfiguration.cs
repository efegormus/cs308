using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table => table.HasCheckConstraint("CK_Users_Role",
            "`Role` IN ('Customer', 'SalesManager', 'ProductManager', 'SupportAgent')"));
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(user => user.Name)
            .HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(user => user.Email)
            .HasColumnType("varchar(254)").HasMaxLength(254).IsRequired();
        builder.Property(user => user.TaxId)
            .HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(user => user.HomeAddress)
            .HasColumnType("varchar(1000)").HasMaxLength(1000).IsRequired();
        builder.Property(user => user.PasswordHash)
            .HasColumnType("varchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(user => user.Role)
            .HasConversion<string>().HasColumnType("varchar(32)").HasMaxLength(32)
            .UseCollation("utf8mb4_bin").HasDefaultValue(UserRole.Customer);
        builder.Property(user => user.SecurityStamp)
            .HasColumnType("varchar(32)").HasMaxLength(32).UseCollation("utf8mb4_bin").IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(user => user.Email).IsUnique();
    }
}
