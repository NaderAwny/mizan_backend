using Mizan.Application.DTOs.Notifications;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class NotificationService : INotificationService
{
    private const int MaxPageSize = 50;
    private const int MinPageSize = 1;

    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedNotificationResponse> GetPagedAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var (items, totalCount) = await _unitOfWork.Notifications.GetPagedByOwnerAsync(
            ownerUserId, page, pageSize, unreadOnly, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedNotificationResponse
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var count = await _unitOfWork.Notifications.GetUnreadCountAsync(ownerUserId, cancellationToken);
        return new UnreadCountResponse { UnreadCount = count };
    }

    public async Task MarkAsReadAsync(Guid ownerUserId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId, ownerUserId, cancellationToken);
        if (notification == null)
        {
            throw new NotFoundException("الإشعار غير موجود");
        }

        notification.MarkAsRead();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Notifications.MarkAllReadAsync(ownerUserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static NotificationResponse MapToResponse(Notification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type,
        Title = notification.Title,
        Message = notification.Message,
        TransactionId = notification.TransactionId,
        InstallmentId = notification.InstallmentId,
        PeriodicReportId = notification.PeriodicReportId,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt
    };
}
