using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IInstallmentReminderLogRepository
{
    Task<bool> ExistsAsync(int installmentId, int daysBeforeDue, CancellationToken cancellationToken = default);
    Task AddAsync(InstallmentReminderLog log, CancellationToken cancellationToken = default);
}
