using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Transactions;

public class InstallmentResponse
{
    public Guid Id { get; set; }
    public int InstallmentNumber { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public InstallmentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
}
