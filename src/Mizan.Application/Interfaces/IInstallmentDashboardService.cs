using Mizan.Application.DTOs.Transactions;

namespace Mizan.Application.Interfaces;

/// <summary>
/// خدمة لوحة متابعة الأقساط والديون الشاملة (Read-only).
/// مسؤولة عن Endpoint-ين:
///   GET /api/installments/dashboard
///   GET /api/installments/history
/// </summary>
public interface IInstallmentDashboardService
{
    /// <summary>
    /// ملخص شامل لكل الأقساط النشطة الخاصة بالمحل/المستخدم.
    /// </summary>
    Task<InstallmentsDashboardDto> GetDashboardAsync(Guid shopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// تايم لاين مُقسَّم صفحات لكل الأقساط الفردية عبر كل العملاء، قابل للفلترة.
    /// </summary>
    /// <param name="shopId">معرف المحل للعزل.</param>
    /// <param name="statusFilter">
    /// فلتر الحالة: all | overdue | dueToday | upcoming | paid.
    /// القيمة الافتراضية "all".
    /// </param>
    /// <param name="page">رقم الصفحة (1-based).</param>
    /// <param name="pageSize">عدد العناصر في الصفحة (1-50).</param>
    Task<PagedInstallmentHistoryResponse> GetHistoryAsync(
        Guid shopId,
        string statusFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
