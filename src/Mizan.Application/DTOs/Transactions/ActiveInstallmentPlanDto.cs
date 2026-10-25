namespace Mizan.Application.DTOs.Transactions;

/// <summary>
/// ملخص خطة أقساط واحدة (مجمَّعة على مستوى Transaction) لـ Dashboard الأقساط.
/// </summary>
public class ActiveInstallmentPlanDto
{
    public Guid TransactionId { get; set; }
    public Guid? ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal PaidPercentage { get; set; }

    public int InstallmentsCount { get; set; }
    public int PaidInstallmentsCount { get; set; }

    public DateTime? NextDueDate { get; set; }
    public decimal? NextDueAmount { get; set; }

    /// <summary>أسوأ حالة بين أقساط الخطة (Overdue &gt; DueToday &gt; OnTrack &gt; Paid).</summary>
    public string Status { get; set; } = string.Empty;

    public bool IsOverdue { get; set; }
    public bool IsDueToday { get; set; }
    public int DaysOverdue { get; set; }
}
