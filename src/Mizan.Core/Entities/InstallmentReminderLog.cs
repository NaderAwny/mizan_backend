namespace Mizan.Core.Entities;

public class InstallmentReminderLog
{
    public int Id { get; private set; }
    public int InstallmentId { get; private set; }
    public int DaysBeforeDue { get; private set; }
    public DateTime SentAt { get; private set; }

    // Navigation property
    public Installment? Installment { get; private set; }

    private InstallmentReminderLog() { } // Required for EF Core

    public static InstallmentReminderLog Create(int installmentId, int daysBeforeDue)
    {
        return new InstallmentReminderLog
        {
            InstallmentId = installmentId,
            DaysBeforeDue = daysBeforeDue,
            SentAt = DateTime.UtcNow
        };
    }
}
