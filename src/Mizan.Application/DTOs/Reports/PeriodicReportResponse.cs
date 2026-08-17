namespace Mizan.Application.DTOs.Reports;

public class PeriodicReportResponse
{
    public int Id { get; set; }
    public int BatchNumber { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal TotalPurchasesAmount { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class PagedPeriodicReportResponse
{
    public List<PeriodicReportResponse> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
