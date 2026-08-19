using System.ComponentModel.DataAnnotations;
using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.VoiceNotes;

public class CreateVoiceNoteRequest
{
    /// <summary>معرف الطرف من Contacts (اختياري)</summary>
    public Guid? ContactId { get; set; }

    /// <summary>اسم الطرف (مطلوب لو ContactId = null)</summary>
    [MaxLength(200, ErrorMessage = "اسم الطرف يجب ألا يتجاوز 200 حرف")]
    public string? PartyName { get; set; }

    /// <summary>نوع العملية: 0=بيع، 1=شراء، 2=تحصيل قسط، 3=سداد قسط</summary>
    [Required]
    public TransactionType OperationType { get; set; } = TransactionType.Sale;

    /// <summary>المبلغ</summary>
    [Required]
    [Range(typeof(decimal), "0.01", "999999999",
        ErrorMessage = "المبلغ يجب أن يكون بين 0.01 و 999,999,999")]
    public decimal Amount { get; set; }

    /// <summary>تاريخ ووقت العملية</summary>
    [Required]
    public DateTime OperationDate { get; set; }

    /// <summary>ملاحظة نصية اختيارية</summary>
    [MaxLength(1000, ErrorMessage = "الملاحظة يجب ألا تتجاوز 1000 حرف")]
    public string? Notes { get; set; }
}
