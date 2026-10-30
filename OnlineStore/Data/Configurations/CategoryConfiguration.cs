using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineStore.Models;

namespace OnlineStore.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).HasColumnType("int").ValueGeneratedOnAdd();
        builder.Property(category => category.Name)
            .HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
        builder.Property(category => category.Description).HasColumnType("text");

        builder.HasIndex(category => category.Name).IsUnique();
    }
}
