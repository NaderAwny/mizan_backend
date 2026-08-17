using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IPeriodicReportRepository
{
    Task<PeriodicReport?> GetByIdAsync(int id, int ownerUserId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PeriodicReport> Items, int TotalCount)> GetPagedByOwnerAsync(
        int ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(PeriodicReport report, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodicReport>> GetUnsentAsync(CancellationToken cancellationToken = default);

    void Update(PeriodicReport report);
}
