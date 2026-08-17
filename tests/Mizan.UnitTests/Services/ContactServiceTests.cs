using Microsoft.EntityFrameworkCore;
using Mizan.Application.DTOs.Contacts;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Infrastructure.Persistence;
using Mizan.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Mizan.UnitTests.Services;

/// <summary>
/// Uses EF Core InMemory database to test ContactService without mocking.
/// Each test gets its own isolated DB context.
/// </summary>
public class ContactServiceTests
{
    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    private static (ContactService Service, MizanDbContext Db) CreateService()
    {
        var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new ContactService(uow);
        return (service, db);
    }

    // ── Security: cross-user isolation ───────────────────────────────────────

    [Fact]
    public async Task GetById_ForAnotherUsersContact_ShouldReturnNotFound()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // Create contact belonging to user 1
        var contact = Contact.Create(user1, "UserOne Contact", null, null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();

        // User 2 tries to read user 1's contact — must NOT find it
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetByIdAsync(ownerUserId: user2, contactId: contact.Id));

        Assert.Contains("Contact not found", ex.Message);
    }

    [Fact]
    public async Task Update_ForAnotherUsersContact_ShouldReturnNotFound_NotForbidden()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var contact = Contact.Create(user1, "Original", null, null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();

        var request = new UpdateContactRequest { Name = "Hacked" };

        // Must throw NotFoundException — not Forbidden — so existence is not leaked
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.UpdateAsync(ownerUserId: user2, contactId: contact.Id, request));
        Assert.Contains("Contact not found", ex.Message);
    }

    [Fact]
    public async Task Deactivate_ForAnotherUsersContact_ShouldReturnNotFound()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var contact = Contact.Create(user1, "Victim Contact", null, null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.DeactivateAsync(ownerUserId: user2, contactId: contact.Id));
        Assert.Contains("Contact not found", ex.Message);
    }

    // ── Pagination clamping ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 20, 1, 20)]    // page < 1 → clamp to 1
    [InlineData(-5, 20, 1, 20)]   // negative page → clamp to 1
    [InlineData(1, 0, 1, 1)]      // pageSize < 1 → clamp to 1
    [InlineData(1, 200, 1, 50)]   // pageSize > 50 → clamp to 50
    [InlineData(1, -10, 1, 1)]    // negative pageSize → clamp to 1
    public async Task GetPaged_OutOfRangePageOrSize_ShouldClampValues(
        int requestedPage, int requestedSize,
        int expectedPage, int expectedSize)
    {
        var (service, _) = CreateService();

        var result = await service.GetPagedAsync(
            ownerUserId: Guid.NewGuid(),
            page: requestedPage,
            pageSize: requestedSize,
            searchTerm: null);

        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedSize, result.PageSize);
    }

    // ── Search filtering ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPaged_WithSearchTerm_ShouldFilterByName()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();

        db.Set<Contact>().AddRange(
            Contact.Create(ownerId, "Ahmed Ali", null, null),
            Contact.Create(ownerId, "Mohamed Hassan", null, null),
            Contact.Create(ownerId, "ahmed omar", null, null),
            Contact.Create(ownerId, "Unrelated Name", null, null)
        );
        await db.SaveChangesAsync();

        var result = await service.GetPagedAsync(ownerId, 1, 10, "ahmed");

        // Should match "Ahmed Ali" and "ahmed omar" (case-insensitive)
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
            Assert.Contains("ahmed", item.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetPaged_SearchExcludesOtherUsersContacts()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // User 1 has a contact named "Ahmed"
        db.Set<Contact>().Add(Contact.Create(user1, "Ahmed Shared", null, null));
        // User 2 also has a contact named "Ahmed"
        db.Set<Contact>().Add(Contact.Create(user2, "Ahmed Other", null, null));
        await db.SaveChangesAsync();

        var result = await service.GetPagedAsync(ownerUserId: user1, 1, 10, "ahmed");

        // User 1 should only see their own contact
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Ahmed Shared", result.Items[0].Name);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidRequest_ShouldReturnContactResponse()
    {
        var (service, _) = CreateService();
        var ownerId = Guid.NewGuid();

        var request = new CreateContactRequest
        {
            Name = "Test Contact",
            PhoneNumber = "01012345678",
            Notes = "Test notes"
        };

        var response = await service.CreateAsync(ownerUserId: ownerId, request);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("Test Contact", response.Name);
        Assert.Equal("01012345678", response.PhoneNumber);
        Assert.Equal("Test notes", response.Notes);
        Assert.True(response.IsActive);
    }

    // ── Deactivate (soft delete) ──────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_OwnContact_ShouldSetIsActiveToFalse()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();

        var contact = Contact.Create(ownerId, "To Deactivate", null, null);
        db.Set<Contact>().Add(contact);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await service.DeactivateAsync(ownerId, contact.Id);

        var stored = await db.Set<Contact>().FindAsync(contact.Id);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
    }

    // ── TotalPages calculation ────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 10, 0)]   // 0 items → 0 pages
    [InlineData(10, 10, 1)]  // exact fit → 1 page
    [InlineData(11, 10, 2)]  // one overflow → 2 pages
    [InlineData(1, 50, 1)]   // single item → 1 page
    public async Task GetPaged_TotalPages_ShouldCalculateCorrectly(
        int totalItems, int pageSize, int expectedPages)
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();

        // Arabic alphabet names to avoid digit-rejection by domain validation
        var namePool = new[]
        {
            "أحمد", "محمد", "علي", "سامي", "خالد", "عمر", "ياسر", "نادر",
            "هاني", "وائل", "طارق", "كريم"
        };
        for (int i = 0; i < totalItems; i++)
            db.Set<Contact>().Add(Contact.Create(ownerId, namePool[i % namePool.Length] + (i > 0 ? new string('ب', i % 5) : ""), null, null));
        await db.SaveChangesAsync();

        var result = await service.GetPagedAsync(ownerId, 1, pageSize, null);

        Assert.Equal(expectedPages, result.TotalPages);
        Assert.Equal(totalItems, result.TotalCount);
    }
}
