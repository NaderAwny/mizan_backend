using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Interfaces;
using Mizan.Core.Enums;

namespace Mizan.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : BaseController
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>POST /api/transactions — إنشاء عملية جديدة (مع خيار الأقساط)</summary>
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
        [FromQuery] int? contactId = null,
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
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var response = await _transactionService.GetByIdAsync(CurrentUserId, id, cancellationToken);
        return Success(response);
    }

    /// <summary>DELETE /api/transactions/{id} — حذف ناعم للعملية وإلغاء أقساطها غير المدفوعة</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await _transactionService.DeactivateAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>POST /api/transactions/{id}/voice-note — إرفاق ملاحظة صوتية</summary>
    [HttpPost("{id:int}/voice-note")]
    public async Task<IActionResult> AttachVoiceNote(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var response = await _transactionService.AttachVoiceNoteAsync(CurrentUserId, id, file, cancellationToken);
        return Success(response, "تم إرفاق الملاحظة الصوتية بنجاح");
    }

    /// <summary>GET /api/transactions/{id}/voice-note — الاستماع للملاحظة الصوتية (Stream)</summary>
    [HttpGet("{id:int}/voice-note")]
    public async Task<IActionResult> GetVoiceNote(int id, CancellationToken cancellationToken)
    {
        var (stream, contentType, fileName) = await _transactionService.GetVoiceNoteStreamAsync(CurrentUserId, id, cancellationToken);
        return File(stream, contentType, fileName, enableRangeProcessing: true);
    }

    /// <summary>POST /api/transactions/{id}/installments/{installmentId}/pay — تسجيل سداد قسط</summary>
    [HttpPost("{id:int}/installments/{installmentId:int}/pay")]
    public async Task<IActionResult> MarkInstallmentPaid(int id, int installmentId, CancellationToken cancellationToken)
    {
        var response = await _transactionService.MarkInstallmentPaidAsync(CurrentUserId, id, installmentId, cancellationToken);
        return Success(response, "تم تسجيل سداد القسط بنجاح");
    }
}
