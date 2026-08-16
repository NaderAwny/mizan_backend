using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

[Authorize]
[EnableRateLimiting("GeneralPolicy")]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : BaseController
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>GET /api/notifications?page=1&amp;pageSize=20&amp;unreadOnly=false — قائمة الإشعارات</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var response = await _notificationService.GetPagedAsync(CurrentUserId, page, pageSize, unreadOnly, cancellationToken);
        return Success(response);
    }

    /// <summary>GET /api/notifications/unread-count — عدد الإشعارات غير المقروءة</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var response = await _notificationService.GetUnreadCountAsync(CurrentUserId, cancellationToken);
        return Success(response);
    }

    /// <summary>POST /api/notifications/{id}/read — تمييز إشعار كمقروء</summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkAsReadAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>POST /api/notifications/read-all — تمييز كل الإشعارات كمقروءة</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }
}
