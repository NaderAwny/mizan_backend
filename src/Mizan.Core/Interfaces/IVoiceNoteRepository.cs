using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IVoiceNoteRepository
{
    /// <summary>يجيب ملاحظة صوتية بالـ ID مع التحقق من الملكية</summary>
    Task<VoiceNote?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// يجيب كل الملاحظات الصوتية للـ shop مرتبة من الأحدث للأقدم (مع Include للـ Contact)
    /// </summary>
    Task<(IReadOnlyList<VoiceNote> Items, int TotalCount)> GetPagedByShopAsync(
        Guid shopId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(VoiceNote voiceNote, CancellationToken cancellationToken = default);
}
