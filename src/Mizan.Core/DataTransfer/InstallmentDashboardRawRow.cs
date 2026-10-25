namespace Mizan.Core.DataTransfer;

/// <summary>
/// Projection خام يُستخدم داخلياً في Repository لتجنب N+1.
/// يجمع بيانات Installment + Transaction + Contact في استعلام EF Core واحد.
/// موضوع هنا في Core لتجنب Circular Dependency مع Application.
/// </summary>
public class InstallmentDashboardRawRow
{
    // ── بيانات Installment ───────────────────────────────────────────────────
    public Guid InstallmentId { get; set; }
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }

    // ── بيانات Transaction ──────────────────────────────────────────────────
    public Guid? ContactId { get; set; }
    public string PartyName { get; set; } = string.Empty;   // fallback لو لا يوجد Contact

    // ── بيانات Contact (nullable) ────────────────────────────────────────────
    public string? ContactName { get; set; }
    public string? PhoneNumber { get; set; }
}
