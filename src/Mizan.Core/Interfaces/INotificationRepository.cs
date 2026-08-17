using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
}
