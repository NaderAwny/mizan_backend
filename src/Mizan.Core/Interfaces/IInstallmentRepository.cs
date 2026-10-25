using Mizan.Core.DataTransfer;
using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IInstallmentRepository
{
    Task<IReadOnlyList<Installment>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<Installment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Installment>> GetPendingByDueRangeAsync(
        DateTime startUtc,
        DateTime endUtcExclusive,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Installment> installments, CancellationToken cancellationToken = default);

    void Update(Installment installment);

    /// <summary>
    /// استعلام واحد فعّال يجلب كل الأقساط النشطة للمحل مع بيانات Transaction والـ Contact
    /// لبناء Dashboard الأقساط — بدون N+1.
    /// </summary>
    Task<IReadOnlyList<InstallmentDashboardRawRow>> GetDashboardRowsByShopAsync(
        Guid shopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// استعلام صفحات فعّال لتايم لاين الأقساط الفردية مع دعم الفلترة.
    /// </summary>
    Task<(IReadOnlyList<InstallmentDashboardRawRow> Items, int TotalCount)> GetHistoryPagedByShopAsync(
        Guid shopId,
        string statusFilter,
        int page,
        int pageSize,
        DateTime today,
        CancellationToken cancellationToken = default);
}
