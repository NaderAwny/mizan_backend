namespace Mizan.Application.DTOs.Transactions;

public class PagedTransactionResponse
{
    public IReadOnlyList<TransactionResponse> Items { get; set; } = Array.Empty<TransactionResponse>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
