using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<Transaction> _dbSet;

    public TransactionRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Transaction>();
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.Contact)
            .Include(t => t.Installments)
            .FirstOrDefaultAsync(t => t.Id == id && t.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByShopAndDateAsync(Guid shopId, DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        var nextDate = targetDate.AddDays(1);

        return await _dbSet
            .Include(t => t.Contact)
            .Include(t => t.Installments)
            .Where(t => t.ShopId == shopId && t.IsActive && t.TransactionDate >= targetDate && t.TransactionDate < nextDate)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByShopAndMonthAsync(Guid shopId, int year, int month, CancellationToken cancellationToken = default)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        return await _dbSet
            .Include(t => t.Contact)
            .Include(t => t.Installments)
            .Where(t => t.ShopId == shopId && t.IsActive && t.TransactionDate >= startDate && t.TransactionDate < endDate)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        Guid? contactId = null,
        TransactionType? type = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(t => t.Contact)
            .Include(t => t.Installments)
            .Where(t => t.OwnerUserId == ownerUserId);

        if (contactId.HasValue)
        {
            query = query.Where(t => t.ContactId == contactId.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= dateFrom.Value.Date);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= dateTo.Value.Date.AddDays(1).AddTicks(-1));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<int> GetActiveCountByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.OwnerUserId == ownerUserId && t.IsActive)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetRecentActiveByOwnerAsync(Guid ownerUserId, int count, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(t => t.Contact)
            .Include(t => t.Installments)
            .Where(t => t.OwnerUserId == ownerUserId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(transaction, cancellationToken);
    }

    public void Update(Transaction transaction)
    {
        _dbSet.Update(transaction);
    }
}
