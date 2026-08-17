using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Transactions;

public class TransactionResponse
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public bool IsInstallment { get; set; }
    public InstallmentPlanMode? InstallmentPlanMode { get; set; }
    public NoteType NoteType { get; set; }
    public string? NoteText { get; set; }
    public bool HasVoiceNote { get; set; }
    public List<InstallmentResponse> Installments { get; set; } = new();

    /// <summary>
    /// Total amount paid so far.
    /// For installment transactions: sum of amounts for Paid installments.
    /// For non-installment transactions: 0 (fully unpaid).
    /// </summary>
    public decimal TotalPaid { get; set; }

    /// <summary>
    /// Total remaining unpaid amount.
    /// For installment transactions: sum of amounts for Pending/Overdue installments.
    /// For non-installment transactions: equal to total Transaction Amount.
    /// </summary>
    public decimal TotalRemaining { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
