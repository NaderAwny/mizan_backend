using Microsoft.EntityFrameworkCore;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Infrastructure.Persistence;
using Xunit;

namespace Mizan.UnitTests.Services;

public class NotificationServiceTests
{
    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    private static (NotificationService Service, MizanDbContext Db) CreateService()
    {
        var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new NotificationService(uow);
        return (service, db);
    }

    [Fact]
    public async Task MarkAsReadAsync_ForAnotherUsersNotification_ThrowsNotFoundException()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // Notification belongs to user 1
        var notification = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "أحمد", 500m, DateTime.UtcNow.Date, 1);
        db.Set<Notification>().Add(notification);
        await db.SaveChangesAsync();

        // User 2 attempts to mark user 1's notification as read
        await Assert.ThrowsAsync<NotFoundException>(
            () => service.MarkAsReadAsync(ownerUserId: user2, notificationId: notification.Id));
    }

    [Fact]
    public async Task MarkAsReadAsync_ForOwnNotification_MarksAsReadSuccessfully()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();

        var notification = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "أحمد", 500m, DateTime.UtcNow.Date, 1);
        db.Set<Notification>().Add(notification);
        await db.SaveChangesAsync();

        await service.MarkAsReadAsync(ownerUserId: user1, notificationId: notification.Id);

        var updated = await db.Set<Notification>().FindAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task GetPagedAsync_ClampsPageAndPageSize()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();

        // Add 60 notifications for user 1
        for (int i = 0; i < 60; i++)
        {
            db.Set<Notification>().Add(
                Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), $"طرف {i}", 100m, DateTime.UtcNow.Date, 1));
        }
        await db.SaveChangesAsync();

        // Request page = -5, pageSize = 200 -> should clamp to page 1, pageSize 50
        var result = await service.GetPagedAsync(ownerUserId: user1, page: -5, pageSize: 200, unreadOnly: false);

        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
        Assert.Equal(50, result.Items.Count);
        Assert.Equal(60, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_WhenUnreadOnlyIsTrue_ReturnsOnlyUnreadNotifications()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();

        var n1 = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "طرف 1", 100m, DateTime.UtcNow.Date, 1);
        var n2 = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "طرف 2", 200m, DateTime.UtcNow.Date, 2);
        n2.MarkAsRead();

        db.Set<Notification>().AddRange(n1, n2);
        await db.SaveChangesAsync();

        var result = await service.GetPagedAsync(ownerUserId: user1, page: 1, pageSize: 20, unreadOnly: true);

        Assert.Single(result.Items);
        Assert.Equal(n1.Id, result.Items[0].Id);
        Assert.False(result.Items[0].IsRead);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();

        var n1 = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "طرف 1", 100m, DateTime.UtcNow.Date, 1);
        var n2 = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "طرف 2", 200m, DateTime.UtcNow.Date, 2);
        var n3 = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "طرف 3", 300m, DateTime.UtcNow.Date, 0);
        n3.MarkAsRead();

        db.Set<Notification>().AddRange(n1, n2, n3);
        await db.SaveChangesAsync();

        var response = await service.GetUnreadCountAsync(ownerUserId: user1);

        Assert.Equal(2, response.UnreadCount);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllNotificationsAsReadForUser()
    {
        var (service, db) = CreateService();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var n1 = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "طرف 1", 100m, DateTime.UtcNow.Date, 1);
        var n2 = Notification.CreateInstallmentReminder(user1, Guid.NewGuid(), Guid.NewGuid(), "طرف 2", 200m, DateTime.UtcNow.Date, 2);
        var n3OtherUser = Notification.CreateInstallmentReminder(user2, Guid.NewGuid(), Guid.NewGuid(), "طرف آخر", 300m, DateTime.UtcNow.Date, 0);

        db.Set<Notification>().AddRange(n1, n2, n3OtherUser);
        await db.SaveChangesAsync();

        await service.MarkAllAsReadAsync(ownerUserId: user1);

        var user1Notifications = await db.Set<Notification>().Where(n => n.OwnerUserId == user1).ToListAsync();
        Assert.All(user1Notifications, n => Assert.True(n.IsRead));

        var user2Notification = await db.Set<Notification>().FindAsync(n3OtherUser.Id);
        Assert.NotNull(user2Notification);
        Assert.False(user2Notification.IsRead);
    }
}
