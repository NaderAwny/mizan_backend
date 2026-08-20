using Microsoft.EntityFrameworkCore;
using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Exceptions;
using Mizan.Infrastructure.Persistence;
using Xunit;

namespace Mizan.UnitTests.Services;

public class TransactionServiceTests
{
    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    private class FakeReportPdfGenerator : IReportPdfGenerator
    {
        public byte[] GenerateReportPdf(Mizan.Application.DTOs.Reports.PeriodicReportPdfModel model) =>
            new byte[] { 0x25, 0x50, 0x44, 0x46 };
    }

    private class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Mizan";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static (TransactionService Service, MizanDbContext Db) CreateService()
    {
        var db = CreateDb();
        var uow = new UnitOfWork(db);
        var options = Microsoft.Extensions.Options.Options.Create(new Mizan.Application.DTOs.Reports.PeriodicReportsOptions());
        var pdfGen = new FakeReportPdfGenerator();
        var emailChannel = new Mizan.Infrastructure.Channels.PeriodicReportEmailChannel();
        var env = new FakeHostEnvironment();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionService>.Instance;
        var service = new TransactionService(uow, options, pdfGen, emailChannel, env, logger);
        return (service, db);
    }

    [Fact]
    public async Task CreateAsync_WithContactOwnedByAnotherUser_ShouldThrowNotFoundException()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // Contact owned by User 1
        var contact = Contact.Create(user1, "User One Contact", null, null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();

        // User 2 tries to create transaction referencing User 1's contact
        var request = new CreateTransactionRequest
        {
            ContactId = contact.Id,
            Type = TransactionType.Sale,
            Amount = 500m,
            TransactionDate = DateTime.UtcNow
        };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(ownerUserId: user2, request));

        Assert.Equal("Contact not found", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WithCustomInstallmentsNotMatchingTotalAmount_ShouldThrowDomainException()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();

        var contact = Contact.Create(user1, "Test Contact", null, null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();

        var request = new CreateTransactionRequest
        {
            ContactId = contact.Id,
            Type = TransactionType.Sale,
            Amount = 1000m, // Total 1000
            TransactionDate = DateTime.UtcNow,
            IsInstallment = true,
            InstallmentPlanMode = InstallmentPlanMode.Custom,
            CustomInstallments = new List<CustomInstallmentItem>
            {
                new() { Amount = 400m, DueDate = DateTime.UtcNow.AddDays(7) },
                new() { Amount = 500m, DueDate = DateTime.UtcNow.AddDays(14) } // Sum = 900 != 1000
            }
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateAsync(ownerUserId: user1, request));

        Assert.Contains("Installment amounts must sum exactly to the total transaction amount", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WithAutomaticInstallments_ShouldGenerateScheduleAndSave()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();

        var contact = Contact.Create(user1, "Installment Customer", null, null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();

        var request = new CreateTransactionRequest
        {
            ContactId = contact.Id,
            Type = TransactionType.Sale,
            Amount = 1200m,
            TransactionDate = DateTime.UtcNow,
            IsInstallment = true,
            InstallmentPlanMode = InstallmentPlanMode.Automatic,
            InstallmentCount = 3,
            FirstInstallmentDate = DateTime.UtcNow.AddDays(7),
            Frequency = InstallmentFrequency.Monthly
        };

        var response = await service.CreateAsync(ownerUserId: user1, request);

        Assert.Equal(1200m, response.Amount);
        Assert.Equal(3, response.Installments.Count);
        Assert.Equal(0m, response.TotalPaid);
        Assert.Equal(1200m, response.TotalRemaining);
    }

    [Fact]
    public async Task GetByIdAsync_WithWrongUser_ShouldThrowNotFoundException()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var contact = Contact.Create(user1, "Owner One Contact", null, null);
        db.Set<Contact>().Add(contact);

        var tx = Transaction.Create(user1, contact.Id, TransactionType.Sale, 500m, DateTime.UtcNow);
        db.Set<Transaction>().Add(tx);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetByIdAsync(ownerUserId: user2, transactionId: tx.Id));

        Assert.Equal("Transaction not found", ex.Message);
    }

    [Fact]
    public async Task DeactivateAsync_OwnTransaction_ShouldVoidPendingInstallments()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();

        var contact = Contact.Create(user1, "Customer", null, null);
        db.Set<Contact>().Add(contact);

        var tx = Transaction.Create(user1, contact.Id, TransactionType.Sale, 1000m, DateTime.UtcNow, isInstallment: true, installmentPlanMode: InstallmentPlanMode.Automatic);
        var installments = Installment.GenerateAutomaticSchedule(tx.Id, 1000m, 2, DateTime.UtcNow.AddDays(7), InstallmentFrequency.Weekly);
        foreach (var inst in installments) tx.Installments.Add(inst);

        db.Set<Transaction>().Add(tx);
        await db.SaveChangesAsync();

        // Mark 1st installment paid
        installments[0].MarkAsPaid();
        await db.SaveChangesAsync();

        await service.DeactivateAsync(ownerUserId: user1, transactionId: tx.Id);

        var stored = await db.Set<Transaction>().Include(t => t.Installments).FirstOrDefaultAsync(t => t.Id == tx.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);

        var instList = stored.Installments.OrderBy(i => i.InstallmentNumber).ToList();
        Assert.Equal(InstallmentStatus.Paid, instList[0].Status);
        Assert.Equal(InstallmentStatus.Voided, instList[1].Status);
    }

    [Fact]
    public async Task MarkInstallmentPaidAsync_WithWrongUser_ShouldThrowNotFoundException()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var contact = Contact.Create(user1, "Customer", null, null);
        db.Set<Contact>().Add(contact);

        var tx = Transaction.Create(user1, contact.Id, TransactionType.Sale, 1000m, DateTime.UtcNow, isInstallment: true, installmentPlanMode: InstallmentPlanMode.Automatic);
        var installments = Installment.GenerateAutomaticSchedule(tx.Id, 1000m, 2, DateTime.UtcNow.AddDays(7), InstallmentFrequency.Weekly);
        foreach (var inst in installments) tx.Installments.Add(inst);

        db.Set<Transaction>().Add(tx);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.MarkInstallmentPaidAsync(ownerUserId: user2, tx.Id, installments[0].Id));

        Assert.Equal("Transaction not found", ex.Message);
    }

    // =========================================================================
    // Feature 1: Statistics / Transactions Unit Tests
    // =========================================================================

    [Fact]
    public async Task CreateTransactionAsync_WithValidData_ShouldCreateAndReturnDto()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();
        var shop = Shop.Create(ownerId, "Al-Mizan Supermarket", "Cairo");
        db.Set<Shop>().Add(shop);
        await db.SaveChangesAsync();

        var dto = new CreateTransactionDto
        {
            PartyName = "محمد أحمد",
            OperationType = TransactionType.Sale,
            Amount = 750.50m,
            PaymentMethod = PaymentMethod.Cash,
            OperationDate = DateTime.UtcNow
        };

        var response = await service.CreateTransactionAsync(shop.Id, dto);

        Assert.NotNull(response);
        Assert.Equal(shop.Id, response.ShopId);
        Assert.Equal("محمد أحمد", response.PartyName);
        Assert.Equal(750.50m, response.Amount);
        Assert.Equal(TransactionType.Sale, response.OperationType);
        Assert.Equal(PaymentMethod.Cash, response.PaymentMethod);

        var saved = await db.Set<Transaction>().FirstOrDefaultAsync(t => t.Id == response.Id);
        Assert.NotNull(saved);
        Assert.Equal(shop.Id, saved!.ShopId);
        Assert.Equal(750.50m, saved.Amount);
    }

    [Fact]
    public async Task CreateTransactionAsync_WithZeroOrNegativeAmount_ShouldThrowDomainException()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();
        var shop = Shop.Create(ownerId, "Al-Mizan Store", "Cairo");
        db.Set<Shop>().Add(shop);
        await db.SaveChangesAsync();

        var dtoZero = new CreateTransactionDto
        {
            PartyName = "عميل",
            OperationType = TransactionType.Sale,
            Amount = 0m,
            PaymentMethod = PaymentMethod.Cash,
            OperationDate = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateTransactionAsync(shop.Id, dtoZero));

        var dtoNeg = new CreateTransactionDto
        {
            PartyName = "عميل",
            OperationType = TransactionType.Sale,
            Amount = -100m,
            PaymentMethod = PaymentMethod.Cash,
            OperationDate = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateTransactionAsync(shop.Id, dtoNeg));
    }

    [Fact]
    public async Task CreateTransactionAsync_WithEmptyPartyNameAndNoContact_ShouldThrowDomainException()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();
        var shop = Shop.Create(ownerId, "Al-Mizan Store", "Cairo");
        db.Set<Shop>().Add(shop);
        await db.SaveChangesAsync();

        var dto = new CreateTransactionDto
        {
            ContactId = null,
            PartyName = "   ",
            OperationType = TransactionType.Sale,
            Amount = 100m,
            PaymentMethod = PaymentMethod.Cash,
            OperationDate = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateTransactionAsync(shop.Id, dto));
    }

    [Fact]
    public async Task GetDailyStatisticsAsync_ShouldCalculateTotalSalesAndPurchasesCorrectly()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();
        var shop = Shop.Create(ownerId, "Al-Mizan Store", "Cairo");
        db.Set<Shop>().Add(shop);

        var today = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
        var yesterday = today.AddDays(-1);

        // Transactions today
        var t1 = Transaction.Create(ownerId, null, TransactionType.Sale, 1000m, today, shopId: shop.Id, partyName: "عميل 1");
        var t2 = Transaction.Create(ownerId, null, TransactionType.InstallmentCollection, 500m, today, shopId: shop.Id, partyName: "عميل 2");
        var t3 = Transaction.Create(ownerId, null, TransactionType.Purchase, 300m, today, shopId: shop.Id, partyName: "مورد 1");
        var t4 = Transaction.Create(ownerId, null, TransactionType.InstallmentPayment, 200m, today, shopId: shop.Id, partyName: "مورد 2");

        // Transaction yesterday (should not be included in today's stats)
        var tYesterday = Transaction.Create(ownerId, null, TransactionType.Sale, 9999m, yesterday, shopId: shop.Id, partyName: "عميل قديم");

        db.Set<Transaction>().AddRange(t1, t2, t3, t4, tYesterday);
        await db.SaveChangesAsync();

        var stats = await service.GetDailyStatisticsAsync(shop.Id, today);

        Assert.Equal(today.Date, stats.Date);
        Assert.Equal(4, stats.OperationsCount);
        Assert.Equal(1500m, stats.TotalSales); // 1000 + 500
        Assert.Equal(500m, stats.TotalPurchases); // 300 + 200
        Assert.Equal(4, stats.Transactions.Count);
    }

    [Fact]
    public async Task GetMonthlyStatisticsAsync_ShouldFilterByMonthAndCalculateTotals()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();
        var shop = Shop.Create(ownerId, "Al-Mizan Store", "Cairo");
        db.Set<Shop>().Add(shop);

        var augustDate1 = new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc);
        var augustDate2 = new DateTime(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc);
        var julyDate = new DateTime(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);

        var t1 = Transaction.Create(ownerId, null, TransactionType.Sale, 2500m, augustDate1, shopId: shop.Id, partyName: "عميل أغسطس 1");
        var t2 = Transaction.Create(ownerId, null, TransactionType.Purchase, 1000m, augustDate2, shopId: shop.Id, partyName: "مورد أغسطس");
        var tJuly = Transaction.Create(ownerId, null, TransactionType.Sale, 5000m, julyDate, shopId: shop.Id, partyName: "عميل يوليو");

        db.Set<Transaction>().AddRange(t1, t2, tJuly);
        await db.SaveChangesAsync();

        var stats = await service.GetMonthlyStatisticsAsync(shop.Id, 2026, 8);

        Assert.Equal(2026, stats.Year);
        Assert.Equal(8, stats.Month);
        Assert.Equal(2, stats.OperationsCount);
        Assert.Equal(2500m, stats.TotalSales);
        Assert.Equal(1000m, stats.TotalPurchases);
        Assert.Equal(2, stats.Transactions.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnTodayStatistics()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();
        var shop = Shop.Create(ownerId, "Al-Mizan Store", "Cairo");
        db.Set<Shop>().Add(shop);

        var now = DateTime.UtcNow;
        var t1 = Transaction.Create(ownerId, null, TransactionType.Sale, 450m, now, shopId: shop.Id, partyName: "عميل اليوم");
        db.Set<Transaction>().Add(t1);
        await db.SaveChangesAsync();

        var summary = await service.GetSummaryAsync(shop.Id);

        Assert.NotNull(summary);
        Assert.Equal(1, summary.OperationsCount);
        Assert.Equal(450m, summary.TotalSales);
        Assert.Equal(0m, summary.TotalPurchases);
    }
}
