using Mizan.Application.DTOs.Transactions;

namespace Mizan.Application.DTOs.Contacts;

public class ContactTransactionsResponse
{
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsVip { get; set; }
    public IReadOnlyList<TransactionResponse> Transactions { get; set; } = new List<TransactionResponse>();
    public int TotalTransactions { get; set; }
    public decimal TotalAmount { get; set; }
}
