namespace Mizan.Application.Interfaces;

public interface IReminderScanner
{
    Task<int> ScanAndProcessRemindersAsync(DateTime? referenceDate = null, CancellationToken cancellationToken = default);
}
