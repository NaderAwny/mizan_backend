using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Notifications;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Infrastructure.Persistence;
using Xunit;

namespace Mizan.UnitTests.Services;

public class ReminderScannerTests
{
    private class TestEmailService : IEmailService
    {
        public bool ShouldSucceed { get; set; } = true;
        public List<ReminderCallRecord> Calls { get; } = new();

        public record ReminderCallRecord(
            string ToEmail,
            string RecipientName,
            string ContactName,
            decimal Amount,
            DateTime DueDate,
            int DaysUntilDue);

        public Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ShouldSucceed);
        }

        public Task<bool> SendInstallmentReminderEmailAsync(
            string toEmail,
            string recipientName,
            string contactName,
            decimal amount,
            DateTime dueDate,
            int daysUntilDue,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new ReminderCallRecord(toEmail, recipientName, contactName, amount, dueDate, daysUntilDue));
            return Task.FromResult(ShouldSucceed);
        }

        public Task<bool> SendPeriodicReportEmailAsync(
            string toEmail,
            string recipientName,
            int batchNumber,
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ShouldSucceed);
        }
    }

    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    private static (ReminderScanner Scanner, MizanDbContext Db, TestEmailService EmailService) CreateScanner(
        RemindersOptions? options = null)
    {
        var db = CreateDb();
        var uow = new UnitOfWork(db);
        var emailService = new TestEmailService();

        var remOptions = Options.Create(options ?? new RemindersOptions
        {
            Enabled = true,
            DaysBeforeDue = new List<int> { 3, 1 },
            CheckIntervalMinutes = 60
        });

        var scanner = new ReminderScanner(
            uow,
            emailService,
            remOptions,
            NullLogger<ReminderScanner>.Instance);

        return (scanner, db, emailService);
    }

    private static async Task<(User User, Contact Contact, Transaction Transaction)> SeedBaseDataAsync(MizanDbContext db)
    {
        var user = User.Create("user@example.com", "نادر", "عوني", "customer");
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        var contact = Contact.Create(user.Id, "محمد علي", "01012345678", null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();

        var transaction = Transaction.Create(
            user.Id,
            contact.Id,
            TransactionType.Sale,
            5000m,
            DateTime.UtcNow,
            NoteType.None,
            null,
            true,
            InstallmentPlanMode.Automatic);
        db.Set<Transaction>().Add(transaction);
        await db.SaveChangesAsync();

        return (user, contact, transaction);
    }

    [Fact]
    public async Task ScanAndProcessRemindersAsync_InstallmentDueIn3Days_IsSelectedAndProcessed()
    {
        var (scanner, db, emailService) = CreateScanner();
        var (user, contact, tx) = await SeedBaseDataAsync(db);

        var today = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var installment = Installment.CreateSingle(tx.Id, 1, 1000m, today.AddDays(3));
        db.Set<Installment>().Add(installment);
        await db.SaveChangesAsync();

        int processed = await scanner.ScanAndProcessRemindersAsync(today);

        Assert.Equal(1, processed);

        // Verify email was sent with daysUntilDue = 3
        var call = Assert.Single(emailService.Calls);
        Assert.Equal(user.Email, call.ToEmail);
        Assert.Equal($"{user.FirstName} {user.LastName}", call.RecipientName);
        Assert.Equal(contact.Name, call.ContactName);
        Assert.Equal(installment.Amount, call.Amount);
        Assert.Equal(installment.DueDate, call.DueDate);
        Assert.Equal(3, call.DaysUntilDue);

        // Verify in-app notification exists
        var notification = await db.Set<Notification>().FirstOrDefaultAsync(n => n.InstallmentId == installment.Id);
        Assert.NotNull(notification);
        Assert.Equal(user.Id, notification.OwnerUserId);

        // Verify reminder log exists
        var log = await db.Set<InstallmentReminderLog>().FirstOrDefaultAsync(l => l.InstallmentId == installment.Id && l.DaysBeforeDue == 3);
        Assert.NotNull(log);
    }

    [Fact]
    public async Task ScanAndProcessRemindersAsync_InstallmentDueIn1Day_IsSelectedAndProcessed()
    {
        var (scanner, db, emailService) = CreateScanner();
        var (user, contact, tx) = await SeedBaseDataAsync(db);

        var today = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var installment = Installment.CreateSingle(tx.Id, 1, 1000m, today.AddDays(1));
        db.Set<Installment>().Add(installment);
        await db.SaveChangesAsync();

        int processed = await scanner.ScanAndProcessRemindersAsync(today);

        Assert.Equal(1, processed);
        var call = Assert.Single(emailService.Calls);
        Assert.Equal(1, call.DaysUntilDue);
    }

    [Fact]
    public async Task ScanAndProcessRemindersAsync_InstallmentDueToday_IsSelectedEvenThough0NotInConfiguredList()
    {
        var (scanner, db, emailService) = CreateScanner(new RemindersOptions
        {
            Enabled = true,
            DaysBeforeDue = new List<int> { 5, 3 }, // 0 is not in the list
            CheckIntervalMinutes = 60
        });

        var (user, contact, tx) = await SeedBaseDataAsync(db);

        var today = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var installment = Installment.CreateSingle(tx.Id, 1, 1000m, today); // Due today (0 days)
        db.Set<Installment>().Add(installment);
        await db.SaveChangesAsync();

        int processed = await scanner.ScanAndProcessRemindersAsync(today);

        Assert.Equal(1, processed);
        var call = Assert.Single(emailService.Calls);
        Assert.Equal(0, call.DaysUntilDue);
    }

    [Fact]
    public async Task ScanAndProcessRemindersAsync_InstallmentDueIn2Days_NotConfiguredStage_IsNotSelected()
    {
        var (scanner, db, emailService) = CreateScanner(new RemindersOptions
        {
            Enabled = true,
            DaysBeforeDue = new List<int> { 3, 1 }, // 2 is not configured
            CheckIntervalMinutes = 60
        });

        var (user, contact, tx) = await SeedBaseDataAsync(db);

        var today = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var installment = Installment.CreateSingle(tx.Id, 1, 1000m, today.AddDays(2));
        db.Set<Installment>().Add(installment);
        await db.SaveChangesAsync();

        int processed = await scanner.ScanAndProcessRemindersAsync(today);

        Assert.Equal(0, processed);
        Assert.Empty(emailService.Calls);
    }

    [Fact]
    public async Task ScanAndProcessRemindersAsync_InstallmentAlreadyLoggedForStage_IsNotSelectedAgain()
    {
        var (scanner, db, emailService) = CreateScanner();
        var (user, contact, tx) = await SeedBaseDataAsync(db);

        var today = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var installment = Installment.CreateSingle(tx.Id, 1, 1000m, today.AddDays(3));
        db.Set<Installment>().Add(installment);
        await db.SaveChangesAsync();

        // Add pre-existing log for this installment at stage 3
        var existingLog = InstallmentReminderLog.Create(installment.Id, 3);
        db.Set<InstallmentReminderLog>().Add(existingLog);
        await db.SaveChangesAsync();

        int processed = await scanner.ScanAndProcessRemindersAsync(today);

        Assert.Equal(0, processed);
        Assert.Empty(emailService.Calls);
    }

    [Fact]
    public async Task ScanAndProcessRemindersAsync_PaidOrVoidedInstallments_AreNeverSelected()
    {
        var (scanner, db, emailService) = CreateScanner();
        var (user, contact, tx) = await SeedBaseDataAsync(db);

        var today = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);

        var paidInstallment = Installment.CreateSingle(tx.Id, 1, 500m, today.AddDays(3));
        paidInstallment.MarkAsPaid();

        var voidedInstallment = Installment.CreateSingle(tx.Id, 2, 500m, today.AddDays(1));
        voidedInstallment.Void();

        db.Set<Installment>().AddRange(paidInstallment, voidedInstallment);
        await db.SaveChangesAsync();

        int processed = await scanner.ScanAndProcessRemindersAsync(today);

        Assert.Equal(0, processed);
        Assert.Empty(emailService.Calls);
    }

    [Fact]
    public async Task ScanAndProcessRemindersAsync_WhenEmailFails_LogIsNotSavedSoItRetriesLater()
    {
        var (scanner, db, emailService) = CreateScanner();
        var (user, contact, tx) = await SeedBaseDataAsync(db);

        // Make email fail
        emailService.ShouldSucceed = false;

        var today = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc);
        var installment = Installment.CreateSingle(tx.Id, 1, 1000m, today.AddDays(3));
        db.Set<Installment>().Add(installment);
        await db.SaveChangesAsync();

        int processed = await scanner.ScanAndProcessRemindersAsync(today);

        Assert.Equal(0, processed);

        // No reminder log should be saved
        bool logExists = await db.Set<InstallmentReminderLog>().AnyAsync(l => l.InstallmentId == installment.Id);
        Assert.False(logExists);

        // No notification should be saved
        bool notificationExists = await db.Set<Notification>().AnyAsync(n => n.InstallmentId == installment.Id);
        Assert.False(notificationExists);
    }
}
