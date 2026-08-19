using Mizan.Application.DTOs.VoiceNotes;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class VoiceNoteService : IVoiceNoteService
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 50;

    // مجلد حفظ الملفات الصوتية داخل السيرفر
    private const string AudioStoragePath = "uploads/voice-notes";

    private readonly IUnitOfWork _unitOfWork;

    public VoiceNoteService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<VoiceNoteResponse> CreateAsync(
        Guid ownerUserId,
        CreateVoiceNoteRequest request,
        Stream audioStream,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        // 1. جيب المحل
        var shop = await _unitOfWork.Shops.GetByOwnerIdAsync(ownerUserId, cancellationToken)
            ?? throw new NotFoundException("المحل", ownerUserId);

        // 2. تحقق من الـ Contact لو موجود
        Contact? contact = null;
        if (request.ContactId.HasValue)
        {
            contact = await _unitOfWork.Contacts.GetByIdAsync(
                request.ContactId.Value, ownerUserId, cancellationToken)
                ?? throw new NotFoundException("الطرف", request.ContactId.Value);
        }

        // 3. حدد اسم الطرف
        var partyName = contact?.Name
            ?? (string.IsNullOrWhiteSpace(request.PartyName)
                ? throw new BadRequestException("يجب تحديد اسم الطرف أو اختيار طرف من جهات الاتصال")
                : request.PartyName.Trim());

        // 4. احفظ ملف الصوت
        var audioPath = await SaveAudioFileAsync(audioStream, originalFileName, cancellationToken);

        // 5. أنشئ الـ Entity وخزّنه
        var voiceNote = VoiceNote.Create(
            shopId:        shop.Id,
            ownerUserId:   ownerUserId,
            contactId:     request.ContactId,
            partyName:     partyName,
            operationType: request.OperationType,
            amount:        request.Amount,
            operationDate: request.OperationDate,
            audioPath:     audioPath,
            notes:         request.Notes);

        await _unitOfWork.VoiceNotes.AddAsync(voiceNote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(voiceNote, contact?.Name);
    }

    public async Task<PagedVoiceNoteResponse> GetPagedAsync(
        Guid ownerUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var shop = await _unitOfWork.Shops.GetByOwnerIdAsync(ownerUserId, cancellationToken)
            ?? throw new NotFoundException("المحل", ownerUserId);

        var (items, total) = await _unitOfWork.VoiceNotes.GetPagedByShopAsync(
            shop.Id, page, pageSize, cancellationToken);

        return new PagedVoiceNoteResponse
        {
            Items      = items.Select(v => MapToResponse(v)).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    public async Task<VoiceNoteResponse> GetByIdAsync(
        Guid ownerUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var voiceNote = await _unitOfWork.VoiceNotes.GetByIdAsync(id, ownerUserId, cancellationToken)
            ?? throw new NotFoundException(nameof(VoiceNote), id);

        return MapToResponse(voiceNote);
    }

    public async Task DeleteAsync(
        Guid ownerUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var voiceNote = await _unitOfWork.VoiceNotes.GetByIdAsync(id, ownerUserId, cancellationToken)
            ?? throw new NotFoundException(nameof(VoiceNote), id);

        voiceNote.Delete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ── Private Helpers ────────────────────────────────────────────────────────

    private static async Task<string> SaveAudioFileAsync(
        Stream audioStream, string originalFileName, CancellationToken cancellationToken)
    {
        // توليد اسم فريد للملف مع الاحتفاظ بالامتداد الأصلي
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var fileName  = $"{Guid.NewGuid()}{extension}";
        var directory = Path.Combine(Directory.GetCurrentDirectory(), AudioStoragePath);

        Directory.CreateDirectory(directory); // ينشئ المجلد لو مش موجود

        var filePath = Path.Combine(directory, fileName);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await audioStream.CopyToAsync(fileStream, cancellationToken);

        // يرجع المسار النسبي (للتخزين في DB والإرسال للـ client)
        return $"/{AudioStoragePath}/{fileName}";
    }

    private static VoiceNoteResponse MapToResponse(VoiceNote v, string? resolvedContactName = null)
    {
        var contactName = resolvedContactName
            ?? v.Contact?.Name
            ?? v.PartyName;

        return new VoiceNoteResponse
        {
            Id         = v.Id,
            AudioPath  = v.AudioPath,
            OperationType = v.OperationType,
            OperationTypeLabel = v.OperationType switch
            {
                TransactionType.Sale                  => "بيع",
                TransactionType.Purchase              => "شراء",
                TransactionType.InstallmentCollection => "تحصيل قسط",
                TransactionType.InstallmentPayment    => "سداد قسط",
                _                                     => v.OperationType.ToString()
            },
            Amount        = v.Amount,
            OperationDate = v.OperationDate,
            ContactName   = contactName,
            ContactId     = v.ContactId,
            Notes         = v.Notes,
            CreatedAt     = v.CreatedAt
        };
    }
}
