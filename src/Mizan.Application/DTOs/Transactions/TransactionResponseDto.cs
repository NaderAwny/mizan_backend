using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Transactions;

public class TransactionResponseDto
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid? ContactId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public TransactionType OperationType { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime OperationDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
