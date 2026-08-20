using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IInstallmentReminderLogRepository
{
    Task<bool> ExistsAsync(Guid installmentId, int daysBeforeDue, CancellationToken cancellationToken = default);
    Task<HashSet<Guid>> GetLoggedInstallmentIdsAsync(IReadOnlyList<Guid> installmentIds, int daysBeforeDue, CancellationToken cancellationToken = default);
    Task AddAsync(InstallmentReminderLog log, CancellationToken cancellationToken = default);
}
