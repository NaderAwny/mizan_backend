using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).UseIdentityColumn();

        builder.Property(s => s.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.HasIndex(s => s.OwnerId)
            .IsUnique();

        builder.Property(s => s.ShopName)
            .HasColumnName("shop_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Address)
            .HasColumnName("address")
            .HasMaxLength(200)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
