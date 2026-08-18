using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mizan.Core.Entities;

namespace Mizan.Infrastructure.Persistence.Configurations;

public class InstallmentReminderLogConfiguration : IEntityTypeConfiguration<InstallmentReminderLog>
{
    public void Configure(EntityTypeBuilder<InstallmentReminderLog> builder)
    {
        builder.ToTable("installment_reminder_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.InstallmentId)
            .HasColumnName("installment_id")
            .IsRequired();

        builder.Property(l => l.DaysBeforeDue)
            .HasColumnName("days_before_due")
            .IsRequired();

        builder.Property(l => l.ContactEmailSent)
            .HasColumnName("contact_email_sent")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(l => l.SentAt)
            .HasColumnName("sent_at")
            .IsRequired();

        builder.HasOne(l => l.Installment)
            .WithMany()
            .HasForeignKey(l => l.InstallmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.InstallmentId, l.DaysBeforeDue })
            .IsUnique();
    }
}
