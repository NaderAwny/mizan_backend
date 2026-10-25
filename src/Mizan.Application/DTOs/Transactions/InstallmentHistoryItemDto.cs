namespace Mizan.Application.DTOs.Transactions;

/// <summary>
/// عنصر واحد في تايم لاين الأقساط (سجل كل الأقساط الفردية عبر كل العملاء).
/// </summary>
public class InstallmentHistoryItemDto
{
    public Guid InstallmentId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid? ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public bool IsDueToday { get; set; }
    public int DaysOverdue { get; set; }
}
