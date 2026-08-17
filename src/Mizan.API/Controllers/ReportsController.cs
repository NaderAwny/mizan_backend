using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

[Authorize]
[EnableRateLimiting("GeneralPolicy")]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : BaseController
{
    private readonly IPeriodicReportService _reportService;

    public ReportsController(IPeriodicReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>GET /api/reports?page=1&amp;pageSize=20 — استرجاع قائمة التقارير الدورية للمستخدم</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await _reportService.GetPagedAsync(CurrentUserId, page, pageSize, cancellationToken);
        return Success(response);
    }

    /// <summary>GET /api/reports/{id} — تفاصيل تقرير دوري محدد</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _reportService.GetByIdAsync(CurrentUserId, id, cancellationToken);
        return Success(response);
    }

    /// <summary>GET /api/reports/{id}/download — تحميل ملف الـ PDF للتقرير الدوري</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken cancellationToken = default)
    {
        var (stream, contentType, fileName) = await _reportService.GetPdfStreamAsync(CurrentUserId, id, cancellationToken);
        return File(stream, contentType, fileName);
    }
}
