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

    public async Task<bool> ExistsAsync(int installmentId, int daysBeforeDue, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(
            l => l.InstallmentId == installmentId && l.DaysBeforeDue == daysBeforeDue,
            cancellationToken);
    }

    public async Task AddAsync(InstallmentReminderLog log, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(log, cancellationToken);
    }
}
