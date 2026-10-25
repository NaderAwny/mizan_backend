using Microsoft.EntityFrameworkCore;
using Mizan.Core.DataTransfer;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class InstallmentRepository : IInstallmentRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<Installment> _dbSet;

    public InstallmentRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Installment>();
    }

    public async Task<IReadOnlyList<Installment>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(i => i.TransactionId == transactionId)
            .OrderBy(i => i.InstallmentNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Installment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(i => i.Transaction)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Installment>> GetPendingByDueRangeAsync(
        DateTime startUtc,
        DateTime endUtcExclusive,
        Guid? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(i => i.Transaction)
                .ThenInclude(t => t!.Owner)
            .Include(i => i.Transaction)
                .ThenInclude(t => t!.Contact)
            .Where(i => i.Status == InstallmentStatus.Pending
                     && i.Transaction != null
                     && i.Transaction.IsActive
                     && i.Transaction.Owner != null
                     && i.Transaction.Owner.IsActive
                     && i.DueDate >= startUtc
                     && i.DueDate < endUtcExclusive
                     && (ownerUserId == null || i.Transaction.OwnerUserId == ownerUserId.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Installment> installments, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(installments, cancellationToken);
    }

    public void Update(Installment installment)
    {
        _dbSet.Update(installment);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InstallmentDashboardRawRow>> GetDashboardRowsByShopAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        // استعلام Projection واحد — لا N+1
        // يجلب كل أقساط المعاملات النشطة الخاصة بالمحل
        var rows = await _dbSet
            .Where(i => i.Transaction != null
                     && i.Transaction.ShopId == shopId
                     && i.Transaction.IsActive
                     && i.Transaction.IsInstallment
                     && i.Status != InstallmentStatus.Voided)
            .Select(i => new InstallmentDashboardRawRow
            {
                InstallmentId = i.Id,
                TransactionId = i.TransactionId,
                Amount = i.Amount,
                DueDate = i.DueDate,
                IsPaid = i.Status == InstallmentStatus.Paid,
                PaidAt = i.PaidAt,
                ContactId = i.Transaction!.ContactId,
                PartyName = i.Transaction!.PartyName,
                ContactName = i.Transaction!.Contact != null ? i.Transaction.Contact.Name : null,
                PhoneNumber = i.Transaction!.Contact != null ? i.Transaction.Contact.PhoneNumber : null
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InstallmentDashboardRawRow> Items, int TotalCount)> GetHistoryPagedByShopAsync(
        Guid shopId,
        string statusFilter,
        int page,
        int pageSize,
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        var todayDate = today.Date;

        var baseQuery = _dbSet
            .Where(i => i.Transaction != null
                     && i.Transaction.ShopId == shopId
                     && i.Transaction.IsActive
                     && i.Transaction.IsInstallment
                     && i.Status != InstallmentStatus.Voided);

        // تطبيق فلتر الحالة على مستوى قاعدة البيانات
        baseQuery = statusFilter.ToLowerInvariant() switch
        {
            "paid" => baseQuery.Where(i => i.Status == InstallmentStatus.Paid),
            "overdue" => baseQuery.Where(i => i.Status != InstallmentStatus.Paid
                                           && i.DueDate.Date < todayDate),
            "duetoday" => baseQuery.Where(i => i.Status != InstallmentStatus.Paid
                                            && i.DueDate.Date == todayDate),
            "upcoming" => baseQuery.Where(i => i.Status != InstallmentStatus.Paid
                                           && i.DueDate.Date > todayDate),
            _ => baseQuery  // "all" أو أي قيمة غير معروفة
        };

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderBy(i => i.DueDate)
            .ThenBy(i => i.TransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InstallmentDashboardRawRow
            {
                InstallmentId = i.Id,
                TransactionId = i.TransactionId,
                Amount = i.Amount,
                DueDate = i.DueDate,
                IsPaid = i.Status == InstallmentStatus.Paid,
                PaidAt = i.PaidAt,
                ContactId = i.Transaction!.ContactId,
                PartyName = i.Transaction!.PartyName,
                ContactName = i.Transaction!.Contact != null ? i.Transaction.Contact.Name : null,
                PhoneNumber = i.Transaction!.Contact != null ? i.Transaction.Contact.PhoneNumber : null
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}

