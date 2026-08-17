using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Reports;

public class PeriodicReportPdfModel
{
    public int BatchNumber { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal TotalPurchasesAmount { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public List<PeriodicReportPdfTransactionItem> Transactions { get; set; } = new();
}

public class PeriodicReportPdfTransactionItem
{
    public string ContactName { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public bool IsInstallment { get; set; }
}
