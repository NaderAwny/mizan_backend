using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Reports;
using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Interfaces;
using Mizan.Infrastructure.Persistence;
using Xunit;

namespace Mizan.UnitTests.Services;

public class PeriodicReportTriggerTests
{
    private class FakeReportPdfGenerator : IReportPdfGenerator
    {
        public int CallCount { get; private set; }
        public PeriodicReportPdfModel? LastModel { get; private set; }

        public byte[] GenerateReportPdf(PeriodicReportPdfModel model)
        {
            CallCount++;
            LastModel = model;
            return new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        }
    }

    private class FakeEmailService : IEmailService
    {
        public int SentReportsCount { get; private set; }

        public Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendInstallmentReminderEmailAsync(
            string toEmail, string recipientName, string contactName, decimal amount, DateTime dueDate, int daysUntilDue, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendPeriodicReportEmailAsync(
            string toEmail, string recipientName, int batchNumber, byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            SentReportsCount++;
            return Task.FromResult(true);
        }
    }

    private static MizanDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new MizanDbContext(options);
    }

    private static IServiceScopeFactory CreateScopeFactory(string dbName, IEmailService emailService)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MizanDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(emailService);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task CreateAsync_When6thTransactionCreated_ShouldNotTriggerPeriodicReport()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var uow = new UnitOfWork(db);
        var pdfGen = new FakeReportPdfGenerator();
        var emailSvc = new FakeEmailService();
        var scopeFactory = CreateScopeFactory(dbName, emailSvc);
        var options = Microsoft.Extensions.Options.Options.Create(new PeriodicReportsOptions { Enabled = true, TransactionThreshold = 7 });
        var service = new TransactionService(uow, options, pdfGen, scopeFactory, NullLogger<TransactionService>.Instance);

        int ownerUserId = 1;
        var contact = Contact.Create(ownerUserId, "عميل تجريبي", null, null);
        db.Set<Contact>().Add(contact);

        // Pre-populate 5 active transactions
        for (int i = 0; i < 5; i++)
        {
            db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Sale, 100, DateTime.UtcNow));
        }
        await db.SaveChangesAsync();

        // Act - Create 6th transaction
        var request = new CreateTransactionRequest
        {
            ContactId = contact.Id,
            Type = TransactionType.Sale,
            Amount = 100,
            TransactionDate = DateTime.UtcNow
        };
        var result = await service.CreateAsync(ownerUserId, request);

        // Assert
        Assert.NotNull(result);
        var reportsCount = await db.Set<PeriodicReport>().CountAsync();
        Assert.Equal(0, reportsCount);
        Assert.Equal(0, pdfGen.CallCount);
    }

    [Fact]
    public async Task CreateAsync_When7thTransactionCreated_ShouldTriggerPeriodicReportAndNotification()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var uow = new UnitOfWork(db);
        var pdfGen = new FakeReportPdfGenerator();
        var emailSvc = new FakeEmailService();
        var scopeFactory = CreateScopeFactory(dbName, emailSvc);
        var options = Microsoft.Extensions.Options.Options.Create(new PeriodicReportsOptions { Enabled = true, TransactionThreshold = 7 });
        var service = new TransactionService(uow, options, pdfGen, scopeFactory, NullLogger<TransactionService>.Instance);

        int ownerUserId = 1;
        var user = User.Create("owner@test.com", "أحمد", "علي", "shop_owner");
        var contact = Contact.Create(ownerUserId, "عميل تجريبي", null, null);
        db.Set<User>().Add(user);
        db.Set<Contact>().Add(contact);

        // Pre-populate 6 active transactions (4 sales totaling 800, 2 purchases totaling 300)
        db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Sale, 200, DateTime.UtcNow));
        db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Sale, 200, DateTime.UtcNow));
        db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Purchase, 150, DateTime.UtcNow));
        db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Sale, 200, DateTime.UtcNow));
        db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Purchase, 150, DateTime.UtcNow));
        db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Sale, 200, DateTime.UtcNow));
        await db.SaveChangesAsync();

        // Act - Create 7th transaction (Sale 300)
        var request = new CreateTransactionRequest
        {
            ContactId = contact.Id,
            Type = TransactionType.Sale,
            Amount = 300,
            TransactionDate = DateTime.UtcNow
        };
        var result = await service.CreateAsync(ownerUserId, request);

        // Assert
        Assert.NotNull(result);
        var reports = await db.Set<PeriodicReport>().ToListAsync();
        Assert.Single(reports);

        var report = reports[0];
        Assert.Equal(ownerUserId, report.OwnerUserId);
        Assert.Equal(1, report.BatchNumber);
        Assert.Equal(7, report.TransactionCount);
        Assert.Equal(1100m, report.TotalSalesAmount); // 200*4 + 300 = 1100
        Assert.Equal(300m, report.TotalPurchasesAmount); // 150 + 150 = 300

        var notifications = await db.Set<Notification>().Where(n => n.Type == NotificationType.PeriodicReportReady).ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(report.Id, notifications[0].PeriodicReportId);
        Assert.Equal(ownerUserId, notifications[0].OwnerUserId);
    }

    [Fact]
    public async Task CreateAsync_When14thTransactionCreated_ShouldTriggerBatchNumber2()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var uow = new UnitOfWork(db);
        var pdfGen = new FakeReportPdfGenerator();
        var emailSvc = new FakeEmailService();
        var scopeFactory = CreateScopeFactory(dbName, emailSvc);
        var options = Microsoft.Extensions.Options.Options.Create(new PeriodicReportsOptions { Enabled = true, TransactionThreshold = 7 });
        var service = new TransactionService(uow, options, pdfGen, scopeFactory, NullLogger<TransactionService>.Instance);

        int ownerUserId = 1;
        var contact = Contact.Create(ownerUserId, "عميل تجريبي", null, null);
        db.Set<Contact>().Add(contact);

        // Pre-populate 13 active transactions
        for (int i = 0; i < 13; i++)
        {
            db.Set<Transaction>().Add(Transaction.Create(ownerUserId, contact.Id, TransactionType.Sale, 100, DateTime.UtcNow));
        }
        // Also simulate batch 1 report already existing
        db.Set<PeriodicReport>().Add(PeriodicReport.Create(ownerUserId, 1, 7, 700, 0, "path1.pdf"));
        await db.SaveChangesAsync();

        // Act - Create 14th transaction
        var request = new CreateTransactionRequest
        {
            ContactId = contact.Id,
            Type = TransactionType.Sale,
            Amount = 100,
            TransactionDate = DateTime.UtcNow
        };
        var result = await service.CreateAsync(ownerUserId, request);

        // Assert
        Assert.NotNull(result);
        var reports = await db.Set<PeriodicReport>().OrderBy(r => r.BatchNumber).ToListAsync();
        Assert.Equal(2, reports.Count);
        Assert.Equal(1, reports[0].BatchNumber);
        Assert.Equal(2, reports[1].BatchNumber);
    }
}
