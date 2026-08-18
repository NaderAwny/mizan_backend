namespace Mizan.Application.DTOs.Transactions;

public class MonthlyStatisticsResponseDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public int OperationsCount { get; set; }
    public List<TransactionResponseDto> Transactions { get; set; } = new();
}
