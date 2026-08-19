using Mizan.Application.DTOs.VoiceNotes;

namespace Mizan.Application.Interfaces;

public interface IVoiceNoteService
{
    /// <summary>ينشئ ملاحظة صوتية جديدة — يحفظ الملف أولاً ثم يحفظ البيانات</summary>
    Task<VoiceNoteResponse> CreateAsync(
        Guid ownerUserId,
        CreateVoiceNoteRequest request,
        Stream audioStream,
        string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>يرجع قائمة مقسّمة لصفحات بكل الملاحظات الصوتية للمحل</summary>
    Task<PagedVoiceNoteResponse> GetPagedAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>يرجع ملاحظة صوتية واحدة بالـ ID</summary>
    Task<VoiceNoteResponse> GetByIdAsync(
        Guid ownerUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>يحذف ملاحظة صوتية (soft delete)</summary>
    Task DeleteAsync(
        Guid ownerUserId,
        Guid id,
        CancellationToken cancellationToken = default);
}
