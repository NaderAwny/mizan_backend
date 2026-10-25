using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

/// <summary>
/// GET /api/installments/dashboard — لوحة متابعة الأقساط والديون الشاملة.
/// GET /api/installments/history  — تايم لاين كل الأقساط الفردية (قابل للفلترة والتصفح).
/// POST /api/installments/{installmentId}/pay — تسجيل سداد قسط (موجود في TransactionsController).
/// </summary>
[Authorize]
[EnableRateLimiting("GeneralPolicy")]
[ApiController]
[Route("api/installments")]
public class InstallmentsController : BaseController
{
    private readonly IInstallmentDashboardService _dashboardService;

    public InstallmentsController(IInstallmentDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// GET /api/installments/dashboard — ملخص شامل لكل الأقساط/الديون الخاصة بالمستخدم الحالي.
    /// يُرجع إجماليات مالية + قائمة خطط نشطة مرتَّبة (Overdue أولاً).
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var shopId = CurrentShopId ?? CurrentUserId;
        var result = await _dashboardService.GetDashboardAsync(shopId, cancellationToken);
        return Success(result);
    }

    /// <summary>
    /// GET /api/installments/history — تايم لاين كل الأقساط الفردية عبر كل العملاء، قابل للفلترة والتصفح.
    /// </summary>
    /// <param name="status">فلتر الحالة: all | overdue | dueToday | upcoming | paid (الافتراضي: all).</param>
    /// <param name="page">رقم الصفحة (الافتراضي: 1).</param>
    /// <param name="pageSize">عدد العناصر في الصفحة — من 1 إلى 50 (الافتراضي: 20).</param>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string status = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var shopId = CurrentShopId ?? CurrentUserId;
        var result = await _dashboardService.GetHistoryAsync(shopId, status, page, pageSize, cancellationToken);
        return Success(result);
    }
}
