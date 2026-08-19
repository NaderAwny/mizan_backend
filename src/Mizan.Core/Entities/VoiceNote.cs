using Mizan.Core.Enums;
using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class VoiceNote
{
    public Guid Id { get; private set; }

    /// <summary>معرف المحل صاحب الملاحظة</summary>
    public Guid ShopId { get; private set; }

    /// <summary>معرف المستخدم (صاحب المحل)</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>معرف الطرف (اختياري — من جدول Contacts)</summary>
    public Guid? ContactId { get; private set; }

    /// <summary>اسم الطرف (يُملأ يدوياً لو ContactId = null)</summary>
    public string PartyName { get; private set; } = string.Empty;

    /// <summary>نوع العملية: بيع / شراء / تحصيل قسط / سداد قسط</summary>
    public TransactionType OperationType { get; private set; }

    /// <summary>المبلغ — يجب أن يكون أكبر من صفر</summary>
    public decimal Amount { get; private set; }

    /// <summary>تاريخ العملية المُسجَّلة في الملاحظة</summary>
    public DateTime OperationDate { get; private set; }

    /// <summary>مسار ملف الصوت المحفوظ على السيرفر</summary>
    public string AudioPath { get; private set; } = string.Empty;

    /// <summary>ملاحظة نصية اختيارية تُرفق مع الصوت</summary>
    public string? Notes { get; private set; }

    /// <summary>هل الملاحظة نشطة (للـ soft delete)</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>تاريخ إنشاء السجل</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>تاريخ آخر تعديل</summary>
    public DateTime UpdatedAt { get; private set; }

    // Navigation properties
    public Shop? Shop { get; private set; }
    public User? Owner { get; private set; }
    public Contact? Contact { get; private set; }

    private VoiceNote() { } // Required for EF Core

    public static VoiceNote Create(
        Guid shopId,
        Guid ownerUserId,
        Guid? contactId,
        string? partyName,
        TransactionType operationType,
        decimal amount,
        DateTime operationDate,
        string audioPath,
        string? notes)
    {
        if (amount <= 0)
            throw new DomainException("المبلغ يجب أن يكون أكبر من صفر");

        if (string.IsNullOrWhiteSpace(audioPath))
            throw new DomainException("مسار ملف الصوت مطلوب");

        if (contactId == null && string.IsNullOrWhiteSpace(partyName))
            throw new DomainException("يجب تحديد الطرف إما عبر ContactId أو اسم الطرف");

        return new VoiceNote
        {
            Id            = Guid.NewGuid(),
            ShopId        = shopId,
            OwnerUserId   = ownerUserId,
            ContactId     = contactId,
            PartyName     = partyName?.Trim() ?? string.Empty,
            OperationType = operationType,
            Amount        = amount,
            OperationDate = operationDate,
            AudioPath     = audioPath.Trim(),
            Notes         = notes?.Trim(),
            IsActive      = true,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };
    }

    public void Delete()
    {
        IsActive  = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
