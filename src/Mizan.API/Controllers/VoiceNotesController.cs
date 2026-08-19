using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.DTOs.VoiceNotes;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

[Authorize]
[EnableRateLimiting("GeneralPolicy")]
[ApiController]
[Route("api/voice-notes")]
[Route("api/[controller]")]
public class VoiceNotesController : BaseController
{
    private readonly IVoiceNoteService _voiceNoteService;

    public VoiceNotesController(IVoiceNoteService voiceNoteService)
    {
        _voiceNoteService = voiceNoteService;
    }

    /// <summary>
    /// POST /api/voice-notes — رفع ملاحظة صوتية جديدة
    /// يُرسَل كـ multipart/form-data:
    /// - audioFile: ملف الصوت (IFormFile)
    /// - contactId: معرف الطرف (اختياري)
    /// - partyName: اسم الطرف (مطلوب لو contactId = null)
    /// - operationType: نوع العملية (0=بيع، 1=شراء، 2=تحصيل قسط، 3=سداد قسط)
    /// - amount: المبلغ
    /// - operationDate: تاريخ العملية
    /// - notes: ملاحظة نصية (اختياري)
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] CreateVoiceNoteRequest request,
        IFormFile audioFile,
        CancellationToken cancellationToken)
    {
        if (audioFile == null || audioFile.Length == 0)
            return BadRequest(new { message = "ملف الصوت مطلوب" });

        await using var stream = audioFile.OpenReadStream();
        var response = await _voiceNoteService.CreateAsync(
            CurrentUserId, request, stream, audioFile.FileName, cancellationToken);

        return Created(response, "تم حفظ الملاحظة الصوتية بنجاح");
    }

    /// <summary>
    /// GET /api/voice-notes?page=1&amp;pageSize=20 — قائمة كل الملاحظات الصوتية
    /// مرتبة من الأحدث للأقدم، مع كل تفاصيل العملية واسم الطرف.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var response = await _voiceNoteService.GetPagedAsync(
            CurrentUserId, page, pageSize, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// GET /api/voice-notes/{id} — تفاصيل ملاحظة صوتية واحدة
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _voiceNoteService.GetByIdAsync(CurrentUserId, id, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// DELETE /api/voice-notes/{id} — حذف ملاحظة صوتية (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _voiceNoteService.DeleteAsync(CurrentUserId, id, cancellationToken);
        return Ok(new { message = "تم حذف الملاحظة الصوتية بنجاح" });
    }
}
