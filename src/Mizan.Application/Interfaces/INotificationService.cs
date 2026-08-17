using Mizan.Application.DTOs.Notifications;

namespace Mizan.Application.Interfaces;

public interface INotificationService
{
    Task<PagedNotificationResponse> GetPagedAsync(Guid ownerUserId, int page, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default);
    Task<UnreadCountResponse> GetUnreadCountAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid ownerUserId, Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid ownerUserId, CancellationToken cancellationToken = default);
}
