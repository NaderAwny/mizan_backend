using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> builder)
    {
        builder.ToTable("installments");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(i => i.TransactionId)
            .HasColumnName("transaction_id")
            .IsRequired();

        builder.Property(i => i.InstallmentNumber)
            .HasColumnName("installment_number")
            .IsRequired();

        builder.Property(i => i.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.DueDate)
            .HasColumnName("due_date")
            .IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(i => i.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(i => new { i.TransactionId, i.InstallmentNumber })
            .IsUnique();

        builder.HasIndex(i => i.DueDate);
    }
}
