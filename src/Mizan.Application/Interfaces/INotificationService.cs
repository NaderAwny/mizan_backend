using Mizan.Application.DTOs.Notifications;

namespace Mizan.Application.Interfaces;

public interface INotificationService
{
    Task<PagedNotificationResponse> GetPagedAsync(int ownerUserId, int page, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default);
    Task<UnreadCountResponse> GetUnreadCountAsync(int ownerUserId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(int ownerUserId, int notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(int ownerUserId, CancellationToken cancellationToken = default);
}
