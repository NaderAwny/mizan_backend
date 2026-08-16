using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<Notification> _dbSet;

    public NotificationRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Notification>();
    }

    public async Task<Notification?> GetByIdAsync(int id, int ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(n => n.Id == id && n.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedByOwnerAsync(
        int ownerUserId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(n => n.OwnerUserId == ownerUserId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .CountAsync(n => n.OwnerUserId == ownerUserId && !n.IsRead, cancellationToken);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(notification, cancellationToken);
    }

    public async Task MarkAllReadAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _dbSet
            .Where(n => n.OwnerUserId == ownerUserId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in unreadNotifications)
        {
            notification.MarkAsRead();
        }
    }
}
