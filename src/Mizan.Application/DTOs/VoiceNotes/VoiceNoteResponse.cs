using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.VoiceNotes;

public class VoiceNoteResponse
{
    /// <summary>معرف الملاحظة الصوتية</summary>
    public Guid Id { get; set; }

    /// <summary>مسار / URL ملف الصوت</summary>
    public string AudioPath { get; set; } = string.Empty;

    /// <summary>نوع العملية (enum value)</summary>
    public TransactionType OperationType { get; set; }

    /// <summary>اسم نوع العملية بالعربي</summary>
    public string OperationTypeLabel { get; set; } = string.Empty;

    /// <summary>المبلغ</summary>
    public decimal Amount { get; set; }

    /// <summary>تاريخ العملية</summary>
    public DateTime OperationDate { get; set; }

    /// <summary>اسم الطرف (من Contact.Name أو PartyName)</summary>
    public string ContactName { get; set; } = string.Empty;

    /// <summary>معرف الطرف (nullable)</summary>
    public Guid? ContactId { get; set; }

    /// <summary>ملاحظة نصية (nullable)</summary>
    public string? Notes { get; set; }

    /// <summary>تاريخ إنشاء السجل</summary>
    public DateTime CreatedAt { get; set; }
}

public class PagedVoiceNoteResponse
{
    public IReadOnlyList<VoiceNoteResponse> Items { get; set; } = new List<VoiceNoteResponse>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
