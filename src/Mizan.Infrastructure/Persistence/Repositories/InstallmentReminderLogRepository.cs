using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class InstallmentReminderLogRepository : IInstallmentReminderLogRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<InstallmentReminderLog> _dbSet;

    public InstallmentReminderLogRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet = context.Set<InstallmentReminderLog>();
    }

    public async Task<bool> ExistsAsync(Guid installmentId, int daysBeforeDue, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(
            l => l.InstallmentId == installmentId && l.DaysBeforeDue == daysBeforeDue,
            cancellationToken);
    }

    public async Task<HashSet<Guid>> GetLoggedInstallmentIdsAsync(IReadOnlyList<Guid> installmentIds, int daysBeforeDue, CancellationToken cancellationToken = default)
    {
        if (installmentIds.Count == 0)
            return new HashSet<Guid>();

        var logged = await _dbSet
            .Where(l => installmentIds.Contains(l.InstallmentId) && l.DaysBeforeDue == daysBeforeDue)
            .Select(l => l.InstallmentId)
            .ToListAsync(cancellationToken);

        return logged.ToHashSet();
    }

    public async Task AddAsync(InstallmentReminderLog log, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(log, cancellationToken);
    }
}
