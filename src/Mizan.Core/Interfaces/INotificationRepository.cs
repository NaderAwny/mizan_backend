using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(int id, int ownerUserId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedByOwnerAsync(
        int ownerUserId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int ownerUserId, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(int ownerUserId, CancellationToken cancellationToken = default);
}
