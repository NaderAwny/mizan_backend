using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class PeriodicReportConfiguration : IEntityTypeConfiguration<PeriodicReport>
{
    public void Configure(EntityTypeBuilder<PeriodicReport> builder)
    {
        builder.ToTable("periodic_reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(r => r.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired();

        builder.Property(r => r.BatchNumber)
            .HasColumnName("batch_number")
            .IsRequired();

        builder.Property(r => r.TransactionCount)
            .HasColumnName("transaction_count")
            .IsRequired();

        builder.Property(r => r.TotalSalesAmount)
            .HasColumnName("total_sales_amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(r => r.TotalPurchasesAmount)
            .HasColumnName("total_purchases_amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(r => r.PdfStoragePath)
            .HasColumnName("pdf_storage_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.EmailSent)
            .HasColumnName("email_sent")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(r => r.GeneratedAt)
            .HasColumnName("generated_at")
            .IsRequired();

        builder.HasOne(r => r.Owner)
            .WithMany()
            .HasForeignKey(r => r.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index for concurrency safety: Exactly one report per batch per user
        builder.HasIndex(r => new { r.OwnerUserId, r.BatchNumber })
            .IsUnique();

        builder.HasIndex(r => new { r.OwnerUserId, r.GeneratedAt });
        builder.HasIndex(r => r.EmailSent);
    }
}
