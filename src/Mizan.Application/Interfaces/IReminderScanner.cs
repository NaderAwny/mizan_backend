namespace Mizan.Application.Interfaces;

public interface IReminderScanner
{
    Task<int> ScanAndProcessRemindersAsync(DateTime? referenceDate = null, CancellationToken cancellationToken = default);
    Task<int> ScanForOwnerAsync(Guid ownerUserId, DateTime? referenceDate = null, CancellationToken cancellationToken = default);
}
