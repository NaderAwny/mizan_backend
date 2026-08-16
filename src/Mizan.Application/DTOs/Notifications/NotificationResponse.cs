using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Notifications;

public class NotificationResponse
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? TransactionId { get; set; }
    public int? InstallmentId { get; set; }
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
