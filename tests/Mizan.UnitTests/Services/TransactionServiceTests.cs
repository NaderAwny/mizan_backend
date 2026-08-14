using Microsoft.EntityFrameworkCore;
using Mizan.Application.DTOs.Transactions;
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

    private static (TransactionService Service, MizanDbContext Db) CreateService()
    {
        var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new TransactionService(uow);
        return (service, db);
    }

    [Fact]
    public async Task CreateAsync_WithContactOwnedByAnotherUser_ShouldThrowNotFoundException()
    {
        var (service, db) = CreateService();

        // Contact owned by User 1
        var contact = Contact.Create(1, "User One Contact", null, null);
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
            service.CreateAsync(ownerUserId: 2, request));

        Assert.Equal("Contact not found", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WithCustomInstallmentsNotMatchingTotalAmount_ShouldThrowDomainException()
    {
        var (service, db) = CreateService();

        var contact = Contact.Create(1, "Test Contact", null, null);
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
            service.CreateAsync(ownerUserId: 1, request));

        Assert.Contains("Installment amounts must sum exactly to the total transaction amount", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WithAutomaticInstallments_ShouldGenerateScheduleAndSave()
    {
        var (service, db) = CreateService();

        var contact = Contact.Create(1, "Installment Customer", null, null);
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

        var response = await service.CreateAsync(ownerUserId: 1, request);

        Assert.Equal(1200m, response.Amount);
        Assert.Equal(3, response.Installments.Count);
        Assert.Equal(0m, response.TotalPaid);
        Assert.Equal(1200m, response.TotalRemaining);
    }

    [Fact]
    public async Task GetByIdAsync_WithWrongUser_ShouldThrowNotFoundException()
    {
        var (service, db) = CreateService();

        var contact = Contact.Create(1, "Owner One Contact", null, null);
        db.Set<Contact>().Add(contact);

        var tx = Transaction.Create(1, contact.Id, TransactionType.Sale, 500m, DateTime.UtcNow);
        db.Set<Transaction>().Add(tx);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetByIdAsync(ownerUserId: 2, transactionId: tx.Id));

        Assert.Equal("Transaction not found", ex.Message);
    }

    [Fact]
    public async Task DeactivateAsync_OwnTransaction_ShouldVoidPendingInstallments()
    {
        var (service, db) = CreateService();

        var contact = Contact.Create(1, "Customer", null, null);
        db.Set<Contact>().Add(contact);

        var tx = Transaction.Create(1, contact.Id, TransactionType.Sale, 1000m, DateTime.UtcNow, isInstallment: true, installmentPlanMode: InstallmentPlanMode.Automatic);
        var installments = Installment.GenerateAutomaticSchedule(tx.Id, 1000m, 2, DateTime.UtcNow.AddDays(7), InstallmentFrequency.Weekly);
        foreach (var inst in installments) tx.Installments.Add(inst);

        db.Set<Transaction>().Add(tx);
        await db.SaveChangesAsync();

        // Mark 1st installment paid
        installments[0].MarkAsPaid();
        await db.SaveChangesAsync();

        await service.DeactivateAsync(ownerUserId: 1, transactionId: tx.Id);

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

        var contact = Contact.Create(1, "Customer", null, null);
        db.Set<Contact>().Add(contact);

        var tx = Transaction.Create(1, contact.Id, TransactionType.Sale, 1000m, DateTime.UtcNow, isInstallment: true, installmentPlanMode: InstallmentPlanMode.Automatic);
        var installments = Installment.GenerateAutomaticSchedule(tx.Id, 1000m, 2, DateTime.UtcNow.AddDays(7), InstallmentFrequency.Weekly);
        foreach (var inst in installments) tx.Installments.Add(inst);

        db.Set<Transaction>().Add(tx);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            service.MarkInstallmentPaidAsync(ownerUserId: 2, tx.Id, installments[0].Id));

        Assert.Equal("Transaction not found", ex.Message);
    }
}
