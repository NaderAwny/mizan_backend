using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.NoteText)
            .HasMaxLength(1000);

        builder.Property(t => t.NoteAudioPath)
            .HasMaxLength(500);

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(t => t.Owner)
            .WithMany()
            .HasForeignKey(t => t.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Contact)
            .WithMany()
            .HasForeignKey(t => t.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Installments)
            .WithOne(i => i.Transaction)
            .HasForeignKey(i => i.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.OwnerUserId, t.TransactionDate });
        builder.HasIndex(t => t.ContactId);
    }
}
