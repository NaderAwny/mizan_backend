using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Interfaces;
using Mizan.Core.Enums;

namespace Mizan.API.Controllers;

[Authorize]
[EnableRateLimiting("GeneralPolicy")]
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : BaseController
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>POST /api/transactions — تسجيل عملية جديدة (مبيعات / مشتريات / تحصيل / سداد / أقساط)</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var response = await _transactionService.CreateAsync(CurrentUserId, request, cancellationToken);
        return Created(response, "تم إنشاء العملية بنجاح");
    }

    /// <summary>GET /api/transactions — قائمة العمليات مع التصفح والفلترة</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? contactId = null,
        [FromQuery] TransactionType? type = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _transactionService.GetPagedAsync(
            CurrentUserId, page, pageSize, contactId, type, dateFrom, dateTo, cancellationToken);
        return Success(response);
    }

    /// <summary>GET /api/transactions/{id} — عملية بالمعرف</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _transactionService.GetByIdAsync(CurrentUserId, id, cancellationToken);
        return Success(response);
    }

    /// <summary>DELETE /api/transactions/{id} — حذف ناعم للعملية وإلغاء أقساطها غير المدفوعة</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await _transactionService.DeactivateAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>POST /api/transactions/{id}/voice-note — إرفاق ملاحظة صوتية</summary>
    [HttpPost("{id:guid}/voice-note")]
    public async Task<IActionResult> AttachVoiceNote(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { statusCode = 400, message = "ملف الصوت مطلوب" });

        await using var stream = file.OpenReadStream();
#pragma warning disable CS0618 // AttachVoiceNoteAsync is obsolete in favor of /api/voice-notes
        var response = await _transactionService.AttachVoiceNoteAsync(
            CurrentUserId, id, stream, file.FileName, file.ContentType, file.Length, cancellationToken);
#pragma warning restore CS0618
        return Success(response, "تم إرفاق الملاحظة الصوتية بنجاح");
    }

    /// <summary>GET /api/transactions/{id}/voice-note — الاستماع للملاحظة الصوتية (Stream)</summary>
    [HttpGet("{id:guid}/voice-note")]
    public async Task<IActionResult> GetVoiceNote(Guid id, CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // GetVoiceNoteStreamAsync is obsolete in favor of /api/voice-notes
        var (stream, contentType, fileName) = await _transactionService.GetVoiceNoteStreamAsync(CurrentUserId, id, cancellationToken);
#pragma warning restore CS0618
        return File(stream, contentType, fileName, enableRangeProcessing: true);
    }

    /// <summary>POST /api/transactions/{id}/installments/{installmentId}/pay — تسجيل سداد قسط</summary>
    [HttpPost("{id:guid}/installments/{installmentId:guid}/pay")]
    public async Task<IActionResult> MarkInstallmentPaid(Guid id, Guid installmentId, CancellationToken cancellationToken)
    {
        var response = await _transactionService.MarkInstallmentPaidAsync(CurrentUserId, id, installmentId, cancellationToken);
        return Success(response, "تم تسجيل سداد القسط بنجاح");
    }

    /// <summary>POST /api/installments/{installmentId}/pay — تسجيل سداد قسط مباشرة بالمعرف</summary>
    [HttpPost("/api/installments/{installmentId:guid}/pay")]
    public async Task<IActionResult> MarkInstallmentPaidDirect(Guid installmentId, CancellationToken cancellationToken)
    {
        var response = await _transactionService.MarkInstallmentPaidAsync(CurrentUserId, installmentId, cancellationToken);
        return Success(response, "تم تسجيل سداد القسط بنجاح");
    }
}
