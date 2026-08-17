namespace Mizan.Core.Entities;

public class InstallmentReminderLog
{
    public Guid Id { get; private set; }
    public Guid InstallmentId { get; private set; }
    public int DaysBeforeDue { get; private set; }
    public DateTime SentAt { get; private set; }

    // Navigation property
    public Installment? Installment { get; private set; }

    private InstallmentReminderLog() { } // Required for EF Core

    public static InstallmentReminderLog Create(Guid installmentId, int daysBeforeDue)
    {
        return new InstallmentReminderLog
        {
            Id = Guid.NewGuid(),
            InstallmentId = installmentId,
            DaysBeforeDue = daysBeforeDue,
            SentAt = DateTime.UtcNow
        };
    }
}
