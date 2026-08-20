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

        public Task<bool> SendInstallmentReminderToContactEmailAsync(
            string toEmail, string contactName, string shopOwnerName, decimal amount, DateTime dueDate, int daysUntilDue, CancellationToken cancellationToken = default) => Task.FromResult(true);

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

    private class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Mizan";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
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
        var emailChannel = new Mizan.Infrastructure.Channels.PeriodicReportEmailChannel();
        var options = Microsoft.Extensions.Options.Options.Create(new PeriodicReportsOptions { Enabled = true, TransactionThreshold = 7 });
        var service = new TransactionService(uow, options, pdfGen, emailChannel, new FakeHostEnvironment(), NullLogger<TransactionService>.Instance);

        Guid ownerUserId = Guid.NewGuid();
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
        var emailChannel = new Mizan.Infrastructure.Channels.PeriodicReportEmailChannel();
        var options = Microsoft.Extensions.Options.Options.Create(new PeriodicReportsOptions { Enabled = true, TransactionThreshold = 7 });
        var service = new TransactionService(uow, options, pdfGen, emailChannel, new FakeHostEnvironment(), NullLogger<TransactionService>.Instance);

        var user = User.Create("owner@test.com", "أحمد", "علي", "shop_owner");
        Guid ownerUserId = user.Id;
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
        var emailChannel = new Mizan.Infrastructure.Channels.PeriodicReportEmailChannel();
        var options = Microsoft.Extensions.Options.Options.Create(new PeriodicReportsOptions { Enabled = true, TransactionThreshold = 7 });
        var service = new TransactionService(uow, options, pdfGen, emailChannel, new FakeHostEnvironment(), NullLogger<TransactionService>.Instance);

        Guid ownerUserId = Guid.NewGuid();
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

    [Fact]
    public async Task CreateAsync_MultiUserIsolation_EachUserGetsTheirOwnReportEvery7Transactions()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var uow = new UnitOfWork(db);
        var pdfGen = new FakeReportPdfGenerator();
        var emailChannel = new Mizan.Infrastructure.Channels.PeriodicReportEmailChannel();
        var options = Microsoft.Extensions.Options.Options.Create(new PeriodicReportsOptions { Enabled = true, TransactionThreshold = 7 });
        var service = new TransactionService(uow, options, pdfGen, emailChannel, new FakeHostEnvironment(), NullLogger<TransactionService>.Instance);

        // Create User 1 and User 2
        var user1 = User.Create("user1@mizan.app", "أحمد", "علي", "shop_owner");
        var user2 = User.Create("user2@mizan.app", "محمود", "حسن", "customer");
        Guid user1Id = user1.Id;
        Guid user2Id = user2.Id;
        var contact1 = Contact.Create(user1Id, "عميل أول", null, null);
        var contact2 = Contact.Create(user2Id, "عميل ثاني", null, null);

        db.Set<User>().AddRange(user1, user2);
        db.Set<Contact>().AddRange(contact1, contact2);
        await db.SaveChangesAsync();

        // User 1 creates 4 transactions
        for (int i = 0; i < 4; i++)
        {
            await service.CreateAsync(user1Id, new CreateTransactionRequest
            {
                ContactId = contact1.Id,
                Type = TransactionType.Sale,
                Amount = 100,
                TransactionDate = DateTime.UtcNow
            });
        }

        // User 2 creates 6 transactions (Total in DB across app = 10 transactions)
        for (int i = 0; i < 6; i++)
        {
            await service.CreateAsync(user2Id, new CreateTransactionRequest
            {
                ContactId = contact2.Id,
                Type = TransactionType.Purchase,
                Amount = 200,
                TransactionDate = DateTime.UtcNow
            });
        }

        // At this point: 10 transactions in DB across app, but neither user has reached 7 individually.
        var initialReports = await db.Set<PeriodicReport>().ToListAsync();
        Assert.Empty(initialReports);

        // User 2 creates their 7th transaction -> Triggers report ONLY for User 2
        await service.CreateAsync(user2Id, new CreateTransactionRequest
        {
            ContactId = contact2.Id,
            Type = TransactionType.Purchase,
            Amount = 200,
            TransactionDate = DateTime.UtcNow
        });

        var user2Reports = await db.Set<PeriodicReport>().Where(r => r.OwnerUserId == user2Id).ToListAsync();
        var user1Reports = await db.Set<PeriodicReport>().Where(r => r.OwnerUserId == user1Id).ToListAsync();

        Assert.Single(user2Reports);
        Assert.Empty(user1Reports);
        Assert.Equal(user2Id, user2Reports[0].OwnerUserId);
        Assert.Equal(1, user2Reports[0].BatchNumber);
        Assert.Equal(7, user2Reports[0].TransactionCount);
        Assert.Equal(1400m, user2Reports[0].TotalPurchasesAmount); // 7 * 200

        // User 1 creates 3 more transactions (Reaching 7 for User 1)
        for (int i = 0; i < 3; i++)
        {
            await service.CreateAsync(user1Id, new CreateTransactionRequest
            {
                ContactId = contact1.Id,
                Type = TransactionType.Sale,
                Amount = 100,
                TransactionDate = DateTime.UtcNow
            });
        }

        // Now both users have reached 7 transactions individually, each having their own Batch 1
        user1Reports = await db.Set<PeriodicReport>().Where(r => r.OwnerUserId == user1Id).ToListAsync();
        user2Reports = await db.Set<PeriodicReport>().Where(r => r.OwnerUserId == user2Id).ToListAsync();

        Assert.Single(user1Reports);
        Assert.Single(user2Reports);
        Assert.Equal(user1Id, user1Reports[0].OwnerUserId);
        Assert.Equal(1, user1Reports[0].BatchNumber);
        Assert.Equal(700m, user1Reports[0].TotalSalesAmount); // 7 * 100
        Assert.Equal(0m, user1Reports[0].TotalPurchasesAmount);
    }
}
