using Microsoft.EntityFrameworkCore;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Infrastructure.Persistence;
using Xunit;

namespace Mizan.UnitTests.Services;

public class PeriodicReportServiceTests
{
    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedReports()
    {
        // Arrange
        using var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new PeriodicReportService(uow);

        int ownerUserId = 1;
        var r1 = PeriodicReport.Create(ownerUserId, 1, 7, 800, 200, "path1.pdf");
        var r2 = PeriodicReport.Create(ownerUserId, 2, 7, 1000, 400, "path2.pdf");
        var otherUserReport = PeriodicReport.Create(99, 1, 7, 300, 50, "path_other.pdf");

        db.Set<PeriodicReport>().AddRange(r1, r2, otherUserReport);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetPagedAsync(ownerUserId, 1, 20);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Items[0].BatchNumber); // Ordered by BatchNumber descending
        Assert.Equal(1, result.Items[1].BatchNumber);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOwned_ShouldReturnReport()
    {
        // Arrange
        using var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new PeriodicReportService(uow);

        int ownerUserId = 1;
        var report = PeriodicReport.Create(ownerUserId, 1, 7, 500, 100, "path.pdf");
        db.Set<PeriodicReport>().Add(report);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(ownerUserId, report.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.BatchNumber);
        Assert.Equal(500, result.TotalSalesAmount);
        Assert.Equal(100, result.TotalPurchasesAmount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotOwned_ShouldThrowNotFoundException()
    {
        // Arrange
        using var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new PeriodicReportService(uow);

        var report = PeriodicReport.Create(2, 1, 7, 500, 100, "path.pdf");
        db.Set<PeriodicReport>().Add(report);
        await db.SaveChangesAsync();

        // Act & Assert (User 1 tries to access User 2's report)
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetByIdAsync(1, report.Id));
    }

    [Fact]
    public async Task GetPdfStreamAsync_WhenFileDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange
        using var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new PeriodicReportService(uow);

        int ownerUserId = 1;
        var report = PeriodicReport.Create(ownerUserId, 1, 7, 500, 100, "non_existent_file.pdf");
        db.Set<PeriodicReport>().Add(report);
        await db.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetPdfStreamAsync(ownerUserId, report.Id));
    }

    [Fact]
    public async Task GetPdfStreamAsync_WhenOwnedAndFileExists_ShouldReturnStream()
    {
        // Arrange
        using var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new PeriodicReportService(uow);

        int ownerUserId = 1;
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3 });

        try
        {
            var report = PeriodicReport.Create(ownerUserId, 1, 7, 500, 100, tempFile);
            db.Set<PeriodicReport>().Add(report);
            await db.SaveChangesAsync();

            // Act
            var (stream, contentType, fileName) = await service.GetPdfStreamAsync(ownerUserId, report.Id);

            // Assert
            Assert.NotNull(stream);
            Assert.Equal("application/pdf", contentType);
            Assert.Equal("mizan-report-batch-1.pdf", fileName);
            stream.Dispose();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
