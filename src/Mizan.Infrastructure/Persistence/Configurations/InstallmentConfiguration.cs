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
        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(i => new { i.TransactionId, i.InstallmentNumber })
            .IsUnique();

        builder.HasIndex(i => i.DueDate);
    }
}
