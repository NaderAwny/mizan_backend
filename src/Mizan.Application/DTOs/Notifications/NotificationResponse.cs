using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Notifications;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public Guid? InstallmentId { get; set; }
    public Guid? PeriodicReportId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PagedNotificationResponse
{
    public List<NotificationResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class UnreadCountResponse
{
    public int UnreadCount { get; set; }
}
