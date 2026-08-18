using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Xunit;

namespace Mizan.UnitTests.Core;

public class NotificationTests
{
    [Fact]
    public void CreateInstallmentReminder_WhenDaysUntilDueGreaterThanZero_ProducesCorrectArabicText()
    {
        // Arrange
        Guid ownerUserId = Guid.NewGuid();
        Guid transactionId = Guid.NewGuid();
        Guid installmentId = Guid.NewGuid();
        string contactName = "أحمد محمد";
        decimal amount = 1500.50m;
        var dueDate = DateTime.UtcNow.Date.AddDays(3);
        int daysUntilDue = 3;

        // Act
        var notification = Notification.CreateInstallmentReminder(
            ownerUserId, transactionId, installmentId, contactName, amount, dueDate, daysUntilDue);

        // Assert
        Assert.Equal(ownerUserId, notification.OwnerUserId);
        Assert.Equal(NotificationType.InstallmentReminder, notification.Type);
        Assert.Equal(transactionId, notification.TransactionId);
        Assert.Equal(installmentId, notification.InstallmentId);
        Assert.False(notification.IsRead);
        Assert.Contains("تذكير بقسط مستحق قريباً", notification.Title);
        Assert.Contains("1500.5", notification.Message);
        Assert.Contains("3 أيام", notification.Message);
        Assert.Contains(contactName, notification.Message);
    }

    [Fact]
    public void CreateInstallmentReminder_WhenDaysUntilDueIsZero_ProducesDueTodayArabicText()
    {
        // Arrange
        Guid ownerUserId = Guid.NewGuid();
        Guid transactionId = Guid.NewGuid();
        Guid installmentId = Guid.NewGuid();
        string contactName = "محمود علي";
        decimal amount = 2000m;
        var dueDate = DateTime.UtcNow.Date;
        int daysUntilDue = 0;

        // Act
        var notification = Notification.CreateInstallmentReminder(
            ownerUserId, transactionId, installmentId, contactName, amount, dueDate, daysUntilDue);

        // Assert
        Assert.Equal("تذكير بقسط مستحق اليوم", notification.Title);
        Assert.Contains("مستحق اليوم", notification.Message);
        Assert.Contains("2000", notification.Message);
        Assert.Contains(contactName, notification.Message);
    }

    [Fact]
    public void MarkAsRead_SetsIsReadToTrue()
    {
        // Arrange
        var notification = Notification.CreateInstallmentReminder(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "عميل", 100m, DateTime.UtcNow.Date, 1);

        // Act
        notification.MarkAsRead();

        // Assert
        Assert.True(notification.IsRead);
    }

    [Fact]
    public void CreateInstallmentReminderForContact_ShouldCreateNotificationWithCorrectType()
    {
        var ownerUserId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var installmentId = Guid.NewGuid();
        var shopOwnerName = "نادر";
        var amount = 500m;
        var dueDate = DateTime.UtcNow.Date;

        var notification = Notification.CreateInstallmentReminderForContact(
            ownerUserId: ownerUserId,
            transactionId: transactionId,
            installmentId: installmentId,
            shopOwnerName: shopOwnerName,
            amount: amount,
            dueDate: dueDate);

        Assert.Equal(NotificationType.InstallmentReminderToContact, notification.Type);
        Assert.Equal(ownerUserId, notification.OwnerUserId);
        Assert.Contains("نادر", notification.Message);
        Assert.Contains("500", notification.Message);
        Assert.Contains("اليوم", notification.Message);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void CreateInstallmentReminderForContact_ShouldContainDateLabel_WhenDueDateInFuture()
    {
        var futureDate = DateTime.UtcNow.Date.AddDays(5);
        var notification = Notification.CreateInstallmentReminderForContact(
            Guid.NewGuid(), null, null, "محمد", 1000m, futureDate);

        Assert.Equal(NotificationType.InstallmentReminderToContact, notification.Type);
        Assert.Contains(futureDate.ToString("yyyy/MM/dd"), notification.Message);
    }
}
