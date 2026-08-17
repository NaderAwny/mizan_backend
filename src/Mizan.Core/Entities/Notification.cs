using Mizan.Core.Enums;
using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class Notification
{
    public int Id { get; private set; }
    public int OwnerUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public int? TransactionId { get; private set; }
    public int? InstallmentId { get; private set; }
    public int? PeriodicReportId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public User? Owner { get; private set; }
    public Transaction? Transaction { get; private set; }
    public Installment? Installment { get; private set; }
    public PeriodicReport? PeriodicReport { get; private set; }

    private Notification() { } // Required for EF Core

    public static Notification CreateInstallmentReminder(
        int ownerUserId,
        int? transactionId,
        int? installmentId,
        string contactName,
        decimal amount,
        DateTime dueDate,
        int daysUntilDue)
    {
        if (ownerUserId <= 0)
            throw new DomainException("معرف المستخدم غير صالح");

        string title;
        string message;

        string formattedAmount = amount.ToString("G29"); // Clean decimal format without trailing zeros

        if (daysUntilDue > 0)
        {
            title = "تذكير بقسط مستحق قريباً";
            string dayText = daysUntilDue == 1 ? "يوم واحد" : daysUntilDue == 2 ? "يومين" : $"{daysUntilDue} أيام";
            message = $"قسط بقيمة {formattedAmount} مستحق خلال {dayText} للطرف {contactName} (تاريخ الاستحقاق: {dueDate:yyyy-MM-dd})";
        }
        else
        {
            title = "تذكير بقسط مستحق اليوم";
            message = $"قسط بقيمة {formattedAmount} مستحق اليوم للطرف {contactName}";
        }

        return new Notification
        {
            OwnerUserId = ownerUserId,
            Type = NotificationType.InstallmentReminder,
            Title = title,
            Message = message,
            TransactionId = transactionId,
            InstallmentId = installmentId,
            PeriodicReportId = null,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Notification CreatePeriodicReportReady(
        int ownerUserId,
        int periodicReportId,
        int batchNumber,
        decimal totalSales,
        decimal totalPurchases,
        int transactionCount)
    {
        if (ownerUserId <= 0)
            throw new DomainException("معرف المستخدم غير صالح");

        if (periodicReportId <= 0)
            throw new DomainException("معرف التقرير الدوري غير صالح");

        string formattedSales = totalSales.ToString("G29");
        string formattedPurchases = totalPurchases.ToString("G29");

        string title = $"التقرير الدوري #{batchNumber} جاهز";
        string message = $"تم إصدار التقرير الدوري للدفعة #{batchNumber} ({transactionCount} عمليات: مبيعات {formattedSales}، مشتريات {formattedPurchases}). يمكنك تحميل التقرير ومراجعته الآن.";

        return new Notification
        {
            OwnerUserId = ownerUserId,
            Type = NotificationType.PeriodicReportReady,
            Title = title,
            Message = message,
            TransactionId = null,
            InstallmentId = null,
            PeriodicReportId = periodicReportId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
