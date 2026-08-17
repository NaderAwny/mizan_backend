using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(n => n.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(n => n.Title)
            .HasColumnName("title")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasColumnName("message")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(n => n.TransactionId)
            .HasColumnName("transaction_id")
            .IsRequired(false);

        builder.Property(n => n.InstallmentId)
            .HasColumnName("installment_id")
            .IsRequired(false);

        builder.Property(n => n.PeriodicReportId)
            .HasColumnName("periodic_report_id")
            .IsRequired(false);

        builder.Property(n => n.IsRead)
            .HasColumnName("is_read")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne(n => n.Owner)
            .WithMany()
            .HasForeignKey(n => n.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Transaction)
            .WithMany()
            .HasForeignKey(n => n.TransactionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(n => n.Installment)
            .WithMany()
            .HasForeignKey(n => n.InstallmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(n => n.PeriodicReport)
            .WithMany()
            .HasForeignKey(n => n.PeriodicReportId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(n => new { n.OwnerUserId, n.CreatedAt });
        builder.HasIndex(n => new { n.OwnerUserId, n.IsRead });
    }
}
