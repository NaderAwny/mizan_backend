using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Exceptions;
using Xunit;

namespace Mizan.UnitTests.Core;

public class PeriodicReportTests
{
    [Fact]
    public void Create_ValidParameters_ShouldInstantiateSuccessfully()
    {
        // Arrange
        Guid ownerUserId = Guid.NewGuid();
        int batchNumber = 1;
        int transactionCount = 7;
        decimal totalSales = 1500.50m;
        decimal totalPurchases = 800.00m;
        string path = @"App_Data\reports\1\test.pdf";

        // Act
        var report = PeriodicReport.Create(ownerUserId, batchNumber, transactionCount, totalSales, totalPurchases, path);

        // Assert
        Assert.NotNull(report);
        Assert.Equal(ownerUserId, report.OwnerUserId);
        Assert.Equal(batchNumber, report.BatchNumber);
        Assert.Equal(transactionCount, report.TransactionCount);
        Assert.Equal(totalSales, report.TotalSalesAmount);
        Assert.Equal(totalPurchases, report.TotalPurchasesAmount);
        Assert.Equal(path, report.PdfStoragePath);
        Assert.False(report.EmailSent);
        Assert.True(report.GeneratedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_EmptyOwnerUserId_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            PeriodicReport.Create(Guid.Empty, 1, 7, 100, 50, "path"));
    }

    [Theory]
    [InlineData(0, 7, "path", 100, 50)]
    [InlineData(-1, 7, "path", 100, 50)]
    [InlineData(1, 0, "path", 100, 50)]
    [InlineData(1, -5, "path", 100, 50)]
    [InlineData(1, 7, "", 100, 50)]
    [InlineData(1, 7, "   ", 100, 50)]
    [InlineData(1, 7, "path", -10, 50)]
    [InlineData(1, 7, "path", 100, -5)]
    public void Create_InvalidParameters_ShouldThrowDomainException(
        int batchNumber,
        int count,
        string path,
        decimal sales,
        decimal purchases)
    {
        Assert.Throws<DomainException>(() =>
            PeriodicReport.Create(Guid.NewGuid(), batchNumber, count, sales, purchases, path));
    }

    [Fact]
    public void MarkEmailSent_ShouldSetEmailSentToTrue()
    {
        // Arrange
        var report = PeriodicReport.Create(Guid.NewGuid(), 1, 7, 500, 200, "path.pdf");
        Assert.False(report.EmailSent);

        // Act
        report.MarkEmailSent();

        // Assert
        Assert.True(report.EmailSent);
    }

    [Fact]
    public void Notification_CreatePeriodicReportReady_ShouldSetCorrectProperties()
    {
        // Arrange & Act
        Guid ownerUserId = Guid.NewGuid();
        Guid reportId = Guid.NewGuid();
        var notification = Notification.CreatePeriodicReportReady(
            ownerUserId: ownerUserId,
            periodicReportId: reportId,
            batchNumber: 2,
            totalSales: 1200m,
            totalPurchases: 300m,
            transactionCount: 7);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(ownerUserId, notification.OwnerUserId);
        Assert.Equal(reportId, notification.PeriodicReportId);
        Assert.Equal(NotificationType.PeriodicReportReady, notification.Type);
        Assert.False(notification.IsRead);
        Assert.Contains("#2", notification.Title);
        Assert.Contains("1200", notification.Message);
        Assert.Contains("300", notification.Message);
    }
}
