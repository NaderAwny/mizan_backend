using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("otp_codes");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).UseIdentityColumn();

        builder.Property(o => o.Email)
            .HasColumnName("email")
            .HasMaxLength(254)
            .IsRequired();

        builder.HasIndex(o => o.Email);

        builder.Property(o => o.Code)
            .HasColumnName("code")
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(o => o.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(o => o.AttemptsCount)
            .HasColumnName("attempts_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(o => o.IsUsed)
            .HasColumnName("is_used")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
