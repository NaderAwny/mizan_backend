namespace Mizan.Application.DTOs.Transactions;

public class DailyStatisticsResponseDto
{
    public DateTime Date { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public int OperationsCount { get; set; }
    public List<TransactionResponseDto> Transactions { get; set; } = new();
}
