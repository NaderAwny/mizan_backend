using System.Security.Cryptography;
using System.Text;
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
    private const string CronKeyHeaderName = "X-Cron-Key";

    private readonly INotificationService _notificationService;
    private readonly IReminderScanner _reminderScanner;
    private readonly IPushNotificationService _pushService;
    private readonly IConfiguration _configuration;

    public NotificationsController(
        INotificationService notificationService,
        IReminderScanner reminderScanner,
        IPushNotificationService pushService,
        IConfiguration configuration)
    {
        _notificationService = notificationService;
        _reminderScanner = reminderScanner;
        _pushService = pushService;
        _configuration = configuration;
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

    /// <summary>POST/PATCH /api/notifications/{id}/read — تمييز إشعار كمقروء</summary>
    [HttpPost("{id:guid}/read")]
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        await _notificationService.MarkAsReadAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>POST/PATCH /api/notifications/read-all — تمييز كل الإشعارات كمقروءة</summary>
    [HttpPost("read-all")]
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>POST /api/notifications/run-reminders-scan — تشغيل فحص تذكيرات الأقساط يدوياً</summary>
    [HttpPost("run-reminders-scan")]
    public async Task<IActionResult> RunRemindersScan(CancellationToken cancellationToken)
    {
        var count = await _reminderScanner.ScanForOwnerAsync(CurrentUserId, null, cancellationToken);
        return Success(new
        {
            remindersProcessed = count,
            timestamp = DateTime.UtcNow
        }, "تم تشغيل فحص تذكيرات الأقساط وإرسال الإشعارات وإيميلات التذكير بنجاح");
    }

    /// <summary>POST /api/notifications/run-reminders-scan-cron — فحص تذكيرات عالمي (كل المستخدمين) للجدولة الخارجية، يتطلب ترويسة X-Cron-Key</summary>
    [AllowAnonymous]
    [HttpPost("run-reminders-scan-cron")]
    public async Task<IActionResult> RunRemindersScanCron(CancellationToken cancellationToken)
    {
        // المفتاح يُقرأ من الإعدادات/متغيرات البيئة (CronApiKey) ولا يُكتب في الكود أبداً.
        // إذا لمُكوَّن المفتاح، تُرفض كل الطلبات.
        var configuredKey = _configuration["CronApiKey"];
        var providedKey = Request.Headers[CronKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(configuredKey) ||
            string.IsNullOrEmpty(providedKey) ||
            !FixedTimeEquals(providedKey, configuredKey))
        {
            return Unauthorized(new { success = false, message = "غير مصرح" });
        }

        var count = await _reminderScanner.ScanAndProcessRemindersAsync(null, cancellationToken);
        return Success(new
        {
            remindersProcessed = count,
            timestamp = DateTime.UtcNow
        }, "تم تشغيل فحص تذكيرات الأقساط وإرسال الإشعارات وإيميلات التذكير بنجاح");
    }

    // مقارنة بثابت الزمن لمنع هجمات التوقيع — نقارن بصمات SHA-256 لتفادي تسريب طول المفتاح.
    private static bool FixedTimeEquals(string provided, string configured)
    {
        byte[] providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        byte[] configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }

    /// <summary>POST /api/notifications/debug-push-variants — إرسال 7 رسائل تجريبية (A..G) بفاصل ثانية، يتطلب ترويسة X-Cron-Key</summary>
    [AllowAnonymous]
    [HttpPost("debug-push-variants")]
    public async Task<IActionResult> DebugPushVariants(
        [FromBody] DebugPushVariantsRequest request,
        CancellationToken cancellationToken)
    {
        var configuredKey = _configuration["CronApiKey"];
        var providedKey = Request.Headers[CronKeyHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(configuredKey) ||
            string.IsNullOrEmpty(providedKey) ||
            !FixedTimeEquals(providedKey, configuredKey))
        {
            return Unauthorized(new { success = false, message = "غير مصرح" });
        }

        if (request == null || request.UserId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "معرّف المستخدم (userId) مطلوب" });
        }

        var results = await _pushService.SendDebugVariantsAsync(request.UserId, cancellationToken);
        return Success(new
        {
            userId = request.UserId,
            results,
            timestamp = DateTime.UtcNow
        }, "تم إرسال رسائل الفحص التجريبية بنجاح");
    }
}

public record DebugPushVariantsRequest(Guid UserId);
