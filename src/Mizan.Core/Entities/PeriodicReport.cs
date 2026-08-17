using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class PeriodicReport
{
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public int BatchNumber { get; private set; }
    public int TransactionCount { get; private set; }
    public decimal TotalSalesAmount { get; private set; }
    public decimal TotalPurchasesAmount { get; private set; }
    public string PdfStoragePath { get; private set; } = string.Empty;
    public bool EmailSent { get; private set; }
    public DateTime GeneratedAt { get; private set; }

    // Navigation property
    public User? Owner { get; private set; }

    private PeriodicReport() { } // Required for EF Core

    public static PeriodicReport Create(
        Guid ownerUserId,
        int batchNumber,
        int transactionCount,
        decimal totalSales,
        decimal totalPurchases,
        string pdfStoragePath)
    {
        if (ownerUserId == Guid.Empty)
            throw new DomainException("معرف المستخدم غير صالح");

        if (batchNumber <= 0)
            throw new DomainException("رقم الدفعة غير صالح");

        if (transactionCount <= 0)
            throw new DomainException("عدد العمليات يجب أن يكون أكبر من صفر");

        if (string.IsNullOrWhiteSpace(pdfStoragePath))
            throw new DomainException("مسار ملف التقرير مطلوب");

        if (totalSales < 0)
            throw new DomainException("إجمالي المبيعات لا يمكن أن يكون سالباً");

        if (totalPurchases < 0)
            throw new DomainException("إجمالي المشتريات لا يمكن أن يكون سالباً");

        return new PeriodicReport
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            BatchNumber = batchNumber,
            TransactionCount = transactionCount,
            TotalSalesAmount = totalSales,
            TotalPurchasesAmount = totalPurchases,
            PdfStoragePath = pdfStoragePath.Trim(),
            EmailSent = false,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public void MarkEmailSent()
    {
        EmailSent = true;
    }
}
