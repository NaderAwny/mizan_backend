using System.Diagnostics;
using System.Text;
using Mizan.Application.DTOs.Reports;
using Mizan.Core.Enums;
using Mizan.Infrastructure.Services.Reports;
using Xunit;

namespace Mizan.UnitTests.Services;

public class ReportPdfGeneratorTests
{
    private readonly ReportPdfGenerator _generator;

    public ReportPdfGeneratorTests()
    {
        _generator = new ReportPdfGenerator();
    }

    [Fact]
    public void GenerateReportPdf_WithMixedTransactionsAndArabicText_ShouldGenerateValidPdf()
    {
        // Arrange
        var model = new PeriodicReportPdfModel
        {
            BatchNumber = 1,
            GeneratedAt = DateTime.UtcNow,
            TransactionCount = 7,
            TotalSalesAmount = 3500.75m,
            TotalPurchasesAmount = 1200.00m,
            RecipientName = "نادر عوني شريف",
            UserEmail = "nader@example.com",
            Transactions = new List<PeriodicReportPdfTransactionItem>
            {
                new() { ContactName = "مؤسسة الأمل للتجارة", Type = TransactionType.Sale, Amount = 1000m, TransactionDate = DateTime.UtcNow.AddDays(-6), IsInstallment = false },
                new() { ContactName = "شركة النور للمقاولات", Type = TransactionType.Purchase, Amount = 500m, TransactionDate = DateTime.UtcNow.AddDays(-5), IsInstallment = false },
                new() { ContactName = "أحمد إبراهيم محمود", Type = TransactionType.Sale, Amount = 750.50m, TransactionDate = DateTime.UtcNow.AddDays(-4), IsInstallment = true },
                new() { ContactName = "سارة حسن علي", Type = TransactionType.Sale, Amount = 250.25m, TransactionDate = DateTime.UtcNow.AddDays(-3), IsInstallment = false },
                new() { ContactName = "مكتبة الشرقية الحديثة", Type = TransactionType.Purchase, Amount = 700m, TransactionDate = DateTime.UtcNow.AddDays(-2), IsInstallment = true },
                new() { ContactName = "محمد عبد السلام", Type = TransactionType.Sale, Amount = 1200m, TransactionDate = DateTime.UtcNow.AddDays(-1), IsInstallment = false },
                new() { ContactName = "محمود السيد طه", Type = TransactionType.Sale, Amount = 300m, TransactionDate = DateTime.UtcNow, IsInstallment = true }
            }
        };

        // Act
        var stopwatch = Stopwatch.StartNew();
        byte[] pdfBytes = _generator.GenerateReportPdf(model);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000, "PDF bytes should be substantial");

        // Verify PDF Header (%PDF)
        string header = Encoding.ASCII.GetString(pdfBytes.Take(4).ToArray());
        Assert.Equal("%PDF", header);

        // Verify fast performance
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"PDF generation should be fast (took {stopwatch.ElapsedMilliseconds}ms)");
    }
}
