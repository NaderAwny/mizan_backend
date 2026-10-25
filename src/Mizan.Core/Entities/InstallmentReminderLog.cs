namespace Mizan.Core.Entities;

public class InstallmentReminderLog
{
    public Guid Id { get; private set; }
    public Guid InstallmentId { get; private set; }
    public int DaysBeforeDue { get; private set; }
    public bool ContactEmailSent { get; private set; } = false;
    public bool OwnerEmailSent { get; private set; } = false;
    public bool PushSent { get; private set; } = false;
    public int Attempts { get; private set; } = 0;
    public DateTime? LastAttemptAt { get; private set; }
    public DateTime SentAt { get; private set; }

    // Navigation property
    public Installment? Installment { get; private set; }

    private InstallmentReminderLog() { } // Required for EF Core

    public static InstallmentReminderLog Create(
        Guid installmentId,
        int daysBeforeDue,
        bool contactEmailSent = false,
        bool ownerEmailSent = false,
        bool pushSent = false)
    {
        return new InstallmentReminderLog
        {
            Id = Guid.NewGuid(),
            InstallmentId = installmentId,
            DaysBeforeDue = daysBeforeDue,
            ContactEmailSent = contactEmailSent,
            OwnerEmailSent = ownerEmailSent,
            PushSent = pushSent,
            Attempts = 0,
            LastAttemptAt = null,
            SentAt = DateTime.UtcNow
        };
    }

    public void MarkOwnerEmailSent()
    {
        OwnerEmailSent = true;
    }

    public void MarkContactEmailSent()
    {
        ContactEmailSent = true;
    }

    public void MarkPushSent()
    {
        PushSent = true;
    }

    public void RecordAttempt(DateTime? attemptTime = null)
    {
        Attempts++;
        LastAttemptAt = attemptTime ?? DateTime.UtcNow;
    }

    public bool IsComplete(bool contactEmailApplicable)
    {
        return OwnerEmailSent && PushSent && (!contactEmailApplicable || ContactEmailSent);
    }

    public bool ShouldRetry(bool contactEmailApplicable, int maxAttempts, TimeSpan backoff, DateTime utcNow)
    {
        if (IsComplete(contactEmailApplicable))
            return false;

        if (Attempts >= maxAttempts)
            return false;

        if (LastAttemptAt.HasValue && (utcNow - LastAttemptAt.Value) < backoff)
            return false;

        return true;
    }
}
