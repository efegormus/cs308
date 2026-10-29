using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(user => user.Name)
            .HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(user => user.Email)
            .HasColumnType("varchar(254)").HasMaxLength(254).IsRequired();
        builder.Property(user => user.PasswordHash)
            .HasColumnType("varchar(512)").HasMaxLength(512).IsRequired();
        builder.Property(user => user.CreatedAt).HasColumnType("datetime(6)").HasPrecision(6);

        builder.HasIndex(user => user.Email).IsUnique();
    }
}
