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

public class ReminderScannerContactEmailTests
{
    private class MockEmailService : IEmailService
    {
        public bool OwnerEmailSucceeds { get; set; } = true;
        public bool ContactEmailSucceeds { get; set; } = true;

        public List<ContactReminderCall> ContactCalls { get; } = new();
        public List<OwnerReminderCall> OwnerCalls { get; } = new();

        public record ContactReminderCall(
            string ToEmail,
            string ContactName,
            string ShopOwnerName,
            decimal Amount,
            DateTime DueDate,
            int DaysUntilDue);

        public record OwnerReminderCall(
            string ToEmail,
            string RecipientName,
            string ContactName,
            decimal Amount,
            DateTime DueDate,
            int DaysUntilDue);

        public Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> SendInstallmentReminderEmailAsync(
            string toEmail,
            string recipientName,
            string contactName,
            decimal amount,
            DateTime dueDate,
            int daysUntilDue,
            CancellationToken cancellationToken = default)
        {
            OwnerCalls.Add(new OwnerReminderCall(toEmail, recipientName, contactName, amount, dueDate, daysUntilDue));
            return Task.FromResult(OwnerEmailSucceeds);
        }

        public Task<bool> SendInstallmentReminderToContactEmailAsync(
            string toEmail,
            string contactName,
            string shopOwnerName,
            decimal amount,
            DateTime dueDate,
            int daysUntilDue,
            CancellationToken cancellationToken = default)
        {
            ContactCalls.Add(new ContactReminderCall(toEmail, contactName, shopOwnerName, amount, dueDate, daysUntilDue));
            return Task.FromResult(ContactEmailSucceeds);
        }

        public Task<bool> SendPeriodicReportEmailAsync(
            string toEmail,
            string recipientName,
            int batchNumber,
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    private static (ReminderScanner Scanner, MizanDbContext Db, MockEmailService EmailService) CreateScanner()
    {
        var db = CreateDb();
        var uow = new UnitOfWork(db);
        var emailService = new MockEmailService();
        var options = Options.Create(new RemindersOptions
        {
            Enabled = true,
            DaysBeforeDue = new List<int> { 0 }
        });
        var scanner = new ReminderScanner(uow, emailService, options, NullLogger<ReminderScanner>.Instance);
        return (scanner, db, emailService);
    }

    private static (User Owner, Contact Contact, Transaction Tx, Installment Inst) SeedData(
        MizanDbContext db, string? contactEmail = null)
    {
        var owner = User.Create("owner@mizan.app", "Nader", "Awny");
        db.Set<User>().Add(owner);

        var contact = Contact.Create(owner.Id, "Mahmoud Hassan", "01012345678", null);
        if (!string.IsNullOrWhiteSpace(contactEmail))
        {
            contact.SetContactEmail(contactEmail);
        }
        db.Set<Contact>().Add(contact);

        var tx = Transaction.Create(
            ownerUserId: owner.Id,
            contactId: contact.Id,
            type: TransactionType.Sale,
            amount: 1000m,
            transactionDate: DateTime.UtcNow.Date,
            isInstallment: true,
            installmentPlanMode: InstallmentPlanMode.Automatic,
            shopId: Guid.NewGuid());
        db.Set<Transaction>().Add(tx);

        var inst = Installment.CreateSingle(tx.Id, 1, 1000m, DateTime.UtcNow.Date);
        db.Set<Installment>().Add(inst);

        db.SaveChanges();
        return (owner, contact, tx, inst);
    }

    [Fact]
    public async Task Scan_WhenContactHasEmail_ShouldSendEmailToBothOwnerAndContact()
    {
        var (scanner, db, emailService) = CreateScanner();
        var (owner, contact, tx, inst) = SeedData(db, "customer@example.com");

        var processed = await scanner.ScanAndProcessRemindersAsync(DateTime.UtcNow.Date);

        Assert.Equal(1, processed);
        Assert.Single(emailService.OwnerCalls);
        Assert.Single(emailService.ContactCalls);

        var contactCall = emailService.ContactCalls[0];
        Assert.Equal("customer@example.com", contactCall.ToEmail);
        Assert.Equal("Mahmoud Hassan", contactCall.ContactName);
        Assert.Equal("Nader Awny", contactCall.ShopOwnerName);
        Assert.Equal(1000m, contactCall.Amount);

        var log = await db.Set<InstallmentReminderLog>().FirstOrDefaultAsync(l => l.InstallmentId == inst.Id);
        Assert.NotNull(log);
        Assert.True(log!.ContactEmailSent);
    }

    [Fact]
    public async Task Scan_WhenContactHasNoEmail_ShouldSendOnlyToOwnerAndSetContactEmailSentFalse()
    {
        var (scanner, db, emailService) = CreateScanner();
        var (owner, contact, tx, inst) = SeedData(db, contactEmail: null);

        var processed = await scanner.ScanAndProcessRemindersAsync(DateTime.UtcNow.Date);

        Assert.Equal(1, processed);
        Assert.Single(emailService.OwnerCalls);
        Assert.Empty(emailService.ContactCalls);

        var log = await db.Set<InstallmentReminderLog>().FirstOrDefaultAsync(l => l.InstallmentId == inst.Id);
        Assert.NotNull(log);
        Assert.False(log!.ContactEmailSent);
    }

    [Fact]
    public async Task Scan_WhenContactEmailFails_ShouldStillRecordReminderLogAndComplete()
    {
        var (scanner, db, emailService) = CreateScanner();
        emailService.ContactEmailSucceeds = false; // Contact email delivery fails
        var (owner, contact, tx, inst) = SeedData(db, "customer@example.com");

        var processed = await scanner.ScanAndProcessRemindersAsync(DateTime.UtcNow.Date);

        Assert.Equal(1, processed);
        Assert.Single(emailService.OwnerCalls);
        Assert.Single(emailService.ContactCalls);

        var log = await db.Set<InstallmentReminderLog>().FirstOrDefaultAsync(l => l.InstallmentId == inst.Id);
        Assert.NotNull(log);
        Assert.False(log!.ContactEmailSent);
    }
}
