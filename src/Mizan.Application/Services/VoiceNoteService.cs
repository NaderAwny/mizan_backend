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
    private readonly IFileStorageService _fileStorage;

    public VoiceNoteService(IUnitOfWork unitOfWork, IFileStorageService fileStorage)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
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

        // 4. تحقق من نوع وحجم ملف الصوت (H4)
        if (audioStream == null)
            throw new BadRequestException("ملف الصوت مطلوب");

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".mp3", ".wav", ".m4a", ".webm", ".mp4", ".ogg", ".aac" };
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
            throw new BadRequestException("صيغة الملف الصوتي غير مدعومة. الصيغ المسموحة: mp3, wav, m4a, webm, mp4");

        const long maxSizeBytes = 10 * 1024 * 1024; // 10 MB
        if (audioStream.CanSeek && audioStream.Length > maxSizeBytes)
            throw new BadRequestException("حجم ملف الصوت يتجاوز الحد الأقصى (10 ميجابايت)");

        // 5. احفظ ملف الصوت عبر IFileStorageService (H5)
        var audioPath = await _fileStorage.UploadAsync(
            audioStream,
            originalFileName,
            "audio/*",
            $"{AudioStoragePath}/{ownerUserId}",
            cancellationToken);

        // 6. أنشئ الـ Entity وخزّنه
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

        // Delete underlying file from storage (H5)
        await _fileStorage.DeleteAsync(voiceNote.AudioPath, cancellationToken);
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
