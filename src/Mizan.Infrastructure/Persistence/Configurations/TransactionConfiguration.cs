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
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(t => t.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired();

        builder.Property(t => t.ContactId)
            .HasColumnName("contact_id")
            .IsRequired();

        builder.Property(t => t.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .HasColumnName("transaction_date")
            .IsRequired();

        builder.Property(t => t.IsInstallment)
            .HasColumnName("is_installment")
            .IsRequired();

        builder.Property(t => t.InstallmentPlanMode)
            .HasColumnName("installment_plan_mode");

        builder.Property(t => t.NoteType)
            .HasColumnName("note_type")
            .IsRequired();

        builder.Property(t => t.NoteText)
            .HasColumnName("note_text")
            .HasMaxLength(1000);

        builder.Property(t => t.NoteAudioPath)
            .HasColumnName("note_audio_path")
            .HasMaxLength(500);

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

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
