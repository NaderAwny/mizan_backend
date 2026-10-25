using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Helpers;
using Mizan.Application.Interfaces;
using Mizan.Core.DataTransfer;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

/// <summary>
/// تنفيذ خدمة لوحة متابعة الأقساط والديون الشاملة (Read-only).
/// </summary>
public class InstallmentDashboardService : IInstallmentDashboardService
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 50;

    private readonly IUnitOfWork _unitOfWork;

    public InstallmentDashboardService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<InstallmentsDashboardDto> GetDashboardAsync(
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        var shop = await ResolveShopIdAsync(shopId, cancellationToken);

        var rows = await _unitOfWork.Installments.GetDashboardRowsByShopAsync(shop, cancellationToken);

        var today = DateTime.UtcNow;

        // ── تجميع على مستوى Transaction (خطة) ────────────────────────────────
        var grouped = rows.GroupBy(r => r.TransactionId);

        var plans = new List<ActiveInstallmentPlanDto>();

        decimal totalDebt = 0m;
        decimal totalPaid = 0m;

        foreach (var group in grouped)
        {
            var installments = group.ToList();

            // حساب حالة كل قسط
            var statusResults = installments
                .Select(i => InstallmentStatusHelper.Evaluate(i.IsPaid, i.DueDate, today))
                .ToList();

            // أسوأ حالة للخطة كلها
            var planStatus = InstallmentStatusHelper.WorstCasePlanStatus(statusResults);

            // مجاميع مالية
            decimal planTotal = installments.Sum(i => i.Amount);
            decimal planPaid = installments.Where(i => i.IsPaid).Sum(i => i.Amount);
            decimal planRemaining = planTotal - planPaid;
            decimal planPct = planTotal == 0m ? 0m : Math.Round(planPaid / planTotal * 100m, 2);

            // الخطط المدفوعة بالكامل لا تظهر في لوحة الأقساط النشطة
            if (planStatus.Status == InstallmentPlanStatusType.Paid)
                continue;

            totalDebt += planTotal;
            totalPaid += planPaid;

            // أقرب قسط غير مدفوع
            var nextInstallment = installments
                .Where(i => !i.IsPaid)
                .OrderBy(i => i.DueDate)
                .FirstOrDefault();

            // معلومات العميل
            var firstRow = installments.First();
            var contactName = !string.IsNullOrWhiteSpace(firstRow.ContactName)
                ? firstRow.ContactName
                : firstRow.PartyName;

            plans.Add(new ActiveInstallmentPlanDto
            {
                TransactionId = group.Key,
                ContactId = firstRow.ContactId,
                ContactName = contactName,
                PhoneNumber = firstRow.PhoneNumber,
                TotalAmount = planTotal,
                PaidAmount = planPaid,
                RemainingAmount = planRemaining,
                PaidPercentage = planPct,
                InstallmentsCount = installments.Count,
                PaidInstallmentsCount = installments.Count(i => i.IsPaid),
                NextDueDate = nextInstallment?.DueDate,
                NextDueAmount = nextInstallment?.Amount,
                Status = planStatus.StatusString,
                IsOverdue = planStatus.IsOverdue,
                IsDueToday = planStatus.IsDueToday,
                DaysOverdue = planStatus.DaysOverdue
            });
        }

        // ── ترتيب: Overdue أولاً → DueToday → الأقرب استحقاقاً ─────────────
        plans = plans
            .OrderByDescending(p => p.IsOverdue)
            .ThenByDescending(p => p.IsDueToday)
            .ThenBy(p => p.NextDueDate ?? DateTime.MaxValue)
            .ToList();

        decimal totalRemaining = totalDebt - totalPaid;
        decimal overallPct = totalDebt == 0m ? 0m : Math.Round(totalPaid / totalDebt * 100m, 2);

        // â”€â”€ Ù…Ø¨Ø§Ù„Øº Ø­Ø³Ø¨ Ø§Ù„Ø­Ø§Ù„Ø© (ØªÙØ­Ø³Ø¨ Ù…Ù† Ø§Ù„Ø£Ù‚Ø³Ø§Ø· Ø§Ù„ÙØ±Ø¯ÙŠØ©) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        decimal totalOverdueAmount  = 0m;
        decimal totalDueTodayAmount = 0m;
        decimal totalUpcomingAmount = 0m;

        foreach (var row in rows)
        {
            if (row.IsPaid) continue;
            var s = InstallmentStatusHelper.Evaluate(row.IsPaid, row.DueDate, today);
            if      (s.IsOverdue)  totalOverdueAmount  += row.Amount;
            else if (s.IsDueToday) totalDueTodayAmount += row.Amount;
            else                   totalUpcomingAmount  += row.Amount;
        }

        return new InstallmentsDashboardDto
        {
            TotalOverdueAmount  = totalOverdueAmount,
            TotalDueTodayAmount = totalDueTodayAmount,
            TotalUpcomingAmount = totalUpcomingAmount,
            TotalPaidAmount     = totalPaid,
            TotalDebtsCount     = plans.Count,
            Plans               = plans
        };
    }

    /// <inheritdoc />
    public async Task<PagedInstallmentHistoryResponse> GetHistoryAsync(
        Guid shopId,
        string statusFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var validFilters = new[] { "all", "overdue", "duetoday", "upcoming", "paid" };
        var normalizedFilter = statusFilter?.ToLowerInvariant() ?? "all";
        if (!validFilters.Contains(normalizedFilter))
            normalizedFilter = "all";

        var shop = await ResolveShopIdAsync(shopId, cancellationToken);
        var today = DateTime.UtcNow;

        var (rawItems, totalCount) = await _unitOfWork.Installments.GetHistoryPagedByShopAsync(
            shop, normalizedFilter, page, pageSize, today, cancellationToken);

        var items = rawItems.Select(r =>
        {
            var statusResult = InstallmentStatusHelper.Evaluate(r.IsPaid, r.DueDate, today);
            var contactName = !string.IsNullOrWhiteSpace(r.ContactName)
                ? r.ContactName
                : r.PartyName;

            return new InstallmentHistoryItemDto
            {
                InstallmentId = r.InstallmentId,
                TransactionId = r.TransactionId,
                ContactId = r.ContactId,
                ContactName = contactName,
                Amount = r.Amount,
                DueDate = r.DueDate,
                IsPaid = r.IsPaid,
                PaidAt = r.PaidAt.HasValue ? DateTime.SpecifyKind(r.PaidAt.Value, DateTimeKind.Utc) : null,
                Status = statusResult.StatusString,
                IsOverdue = statusResult.IsOverdue,
                IsDueToday = statusResult.IsDueToday,
                DaysOverdue = statusResult.DaysOverdue
            };
        }).ToList();

        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedInstallmentHistoryResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// يحوّل shopId أو ownerId إلى shopId الفعلي، متوافقاً مع نفس منطق GetShopOrThrowAsync في TransactionService.
    /// </summary>
    private async Task<Guid> ResolveShopIdAsync(Guid shopOrOwnerId, CancellationToken cancellationToken)
    {
        if (shopOrOwnerId == Guid.Empty)
            throw new BadRequestException("معرف المحل مطلوب");

        var shop = await _unitOfWork.Shops.GetByIdAsync(shopOrOwnerId, cancellationToken)
                ?? await _unitOfWork.Shops.GetByOwnerIdAsync(shopOrOwnerId, cancellationToken);

        if (shop == null)
            throw new NotFoundException("المحل", shopOrOwnerId);

        return shop.Id;
    }
}
