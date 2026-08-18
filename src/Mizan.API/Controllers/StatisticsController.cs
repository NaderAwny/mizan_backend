using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

[Authorize]
[EnableRateLimiting("GeneralPolicy")]
[ApiController]
[Route("api/[controller]")]
public class StatisticsController : BaseController
{
    private readonly ITransactionService _transactionService;

    public StatisticsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>GET /api/statistics/daily?date=YYYY-MM-DD — إحصائيات يوم محدد</summary>
    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateTime.UtcNow.Date;
        var shopId = CurrentShopId ?? CurrentUserId;
        var response = await _transactionService.GetDailyStatisticsAsync(shopId, targetDate, cancellationToken);
        return Success(response);
    }

    /// <summary>GET /api/statistics/monthly?year=YYYY&amp;month=MM — إحصائيات شهر محدد</summary>
    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthly([FromQuery] int? year, [FromQuery] int? month, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        int targetYear = year ?? now.Year;
        int targetMonth = month ?? now.Month;
        var shopId = CurrentShopId ?? CurrentUserId;

        var response = await _transactionService.GetMonthlyStatisticsAsync(shopId, targetYear, targetMonth, cancellationToken);
        return Success(response);
    }

    /// <summary>GET /api/statistics/summary — إحصائيات اليوم الحالي (افتراضياً)</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var shopId = CurrentShopId ?? CurrentUserId;
        var response = await _transactionService.GetSummaryAsync(shopId, cancellationToken);
        return Success(response);
    }
}
