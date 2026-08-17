using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class PeriodicReportRepository : IPeriodicReportRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<PeriodicReport> _dbSet;

    public PeriodicReportRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet = context.Set<PeriodicReport>();
    }

    public async Task<PeriodicReport?> GetByIdAsync(int id, int ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task<(IReadOnlyList<PeriodicReport> Items, int TotalCount)> GetPagedByOwnerAsync(
        int ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Where(r => r.OwnerUserId == ownerUserId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.BatchNumber)
            .ThenByDescending(r => r.GeneratedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(PeriodicReport report, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(report, cancellationToken);
    }

    public async Task<IReadOnlyList<PeriodicReport>> GetUnsentAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Owner)
            .Where(r => !r.EmailSent)
            .OrderBy(r => r.GeneratedAt)
            .ToListAsync(cancellationToken);
    }

    public void Update(PeriodicReport report)
    {
        _dbSet.Update(report);
    }
}
