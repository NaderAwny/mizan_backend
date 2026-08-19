using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class VoiceNoteConfiguration : IEntityTypeConfiguration<VoiceNote>
{
    public void Configure(EntityTypeBuilder<VoiceNote> builder)
    {
        builder.ToTable("voice_notes");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(v => v.ShopId)
            .HasColumnName("shop_id")
            .IsRequired();

        builder.Property(v => v.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired();

        builder.Property(v => v.ContactId)
            .HasColumnName("contact_id")
            .IsRequired(false);

        builder.Property(v => v.PartyName)
            .HasColumnName("party_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.OperationType)
            .HasColumnName("operation_type")
            .IsRequired();

        builder.Property(v => v.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(v => v.OperationDate)
            .HasColumnName("operation_date")
            .IsRequired();

        builder.Property(v => v.AudioPath)
            .HasColumnName("audio_path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(v => v.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(v => v.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Relations
        builder.HasOne(v => v.Shop)
            .WithMany()
            .HasForeignKey(v => v.ShopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Owner)
            .WithMany()
            .HasForeignKey(v => v.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(v => v.Contact)
            .WithMany()
            .HasForeignKey(v => v.ContactId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(v => new { v.ShopId, v.IsActive })
            .HasDatabaseName("IX_voice_notes_shop_id_is_active");

        builder.HasIndex(v => v.OperationDate)
            .HasDatabaseName("IX_voice_notes_operation_date");
    }
}
