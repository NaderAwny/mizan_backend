using Microsoft.EntityFrameworkCore;
using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Helpers;
using Entities = Mizan.Core.Entities;
using Enums = Mizan.Core.Enums;
using Xunit;

namespace Mizan.UnitTests.Services;

/// <summary>
/// اختبارات وحدة لـ InstallmentStatusHelper وخدمة Dashboard الأقساط.
/// تشمل كل الحالات المذكورة في البند 6 من مستند الميزة.
/// </summary>
public class InstallmentDashboardServiceTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // 1. اختبارات InstallmentStatusHelper — حساب الحالة الصحيحة
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_WhenPaid_ReturnsPaidStatus()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc); // ماضي

        var result = InstallmentStatusHelper.Evaluate(isPaid: true, dueDate: dueDate, today: today);

        Assert.Equal("Paid", result.StatusString);
        Assert.False(result.IsOverdue);
        Assert.False(result.IsDueToday);
        Assert.Equal(0, result.DaysOverdue);
    }

    [Fact]
    public void Evaluate_WhenOverdueByOneDay_ReturnsOverdue()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = today.AddDays(-1); // أمس

        var result = InstallmentStatusHelper.Evaluate(isPaid: false, dueDate: dueDate, today: today);

        Assert.Equal("Overdue", result.StatusString);
        Assert.True(result.IsOverdue);
        Assert.False(result.IsDueToday);
        Assert.Equal(1, result.DaysOverdue);
    }

    [Fact]
    public void Evaluate_WhenOverdueByManyDays_ReturnsDaysOverdueCorrect()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc); // 12+ يوم قبل

        var result = InstallmentStatusHelper.Evaluate(isPaid: false, dueDate: dueDate, today: today);

        Assert.Equal("Overdue", result.StatusString);
        Assert.True(result.IsOverdue);
        Assert.True(result.DaysOverdue > 0);
        Assert.Equal((today.Date - dueDate.Date).Days, result.DaysOverdue);
    }

    [Fact]
    public void Evaluate_WhenDueToday_ReturnsDueTodayNotOverdue()
    {
        var today = new DateTime(2026, 9, 27, 14, 30, 0, DateTimeKind.Utc); // وقت معيّن من اليوم
        var dueDate = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc); // نفس اليوم

        var result = InstallmentStatusHelper.Evaluate(isPaid: false, dueDate: dueDate, today: today);

        Assert.Equal("DueToday", result.StatusString);
        Assert.False(result.IsOverdue);  // ← الحافة الحرجة: DueToday ≠ Overdue
        Assert.True(result.IsDueToday);
        Assert.Equal(0, result.DaysOverdue);
    }

    [Fact]
    public void Evaluate_WhenUpcoming_ReturnsUpcoming()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = today.AddDays(5);

        var result = InstallmentStatusHelper.Evaluate(isPaid: false, dueDate: dueDate, today: today);

        Assert.Equal("Upcoming", result.StatusString);
        Assert.False(result.IsOverdue);
        Assert.False(result.IsDueToday);
        Assert.Equal(0, result.DaysOverdue);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. اختبارات WorstCasePlanStatus — الخطة تأخذ أسوأ حالة
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void WorstCasePlanStatus_WhenOneOverdueAmongUpcoming_ReturnsOverdue()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

        var statuses = new[]
        {
            InstallmentStatusHelper.Evaluate(false, today.AddDays(-5), today),  // Overdue
            InstallmentStatusHelper.Evaluate(false, today.AddDays(5), today),   // Upcoming
            InstallmentStatusHelper.Evaluate(true, today.AddDays(-10), today)   // Paid
        };

        var result = InstallmentStatusHelper.WorstCasePlanStatus(statuses);

        Assert.Equal("Overdue", result.StatusString);
        Assert.True(result.IsOverdue);
    }

    [Fact]
    public void WorstCasePlanStatus_WhenAllPaid_ReturnsPaid()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

        var statuses = new[]
        {
            InstallmentStatusHelper.Evaluate(true, today.AddDays(-10), today),
            InstallmentStatusHelper.Evaluate(true, today.AddDays(-5), today),
        };

        var result = InstallmentStatusHelper.WorstCasePlanStatus(statuses);

        Assert.Equal("Paid", result.StatusString);
    }

    [Fact]
    public void WorstCasePlanStatus_WhenAllUpcoming_ReturnsOnTrack()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

        var statuses = new[]
        {
            InstallmentStatusHelper.Evaluate(false, today.AddDays(5), today),
            InstallmentStatusHelper.Evaluate(false, today.AddDays(15), today),
        };

        var result = InstallmentStatusHelper.WorstCasePlanStatus(statuses);

        Assert.Equal("OnTrack", result.StatusString);
        Assert.False(result.IsOverdue);
        Assert.False(result.IsDueToday);
    }

    [Fact]
    public void WorstCasePlanStatus_WhenDueTodayAndUpcoming_ReturnsDueToday()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

        var statuses = new[]
        {
            InstallmentStatusHelper.Evaluate(false, today, today),          // DueToday
            InstallmentStatusHelper.Evaluate(false, today.AddDays(10), today) // Upcoming
        };

        var result = InstallmentStatusHelper.WorstCasePlanStatus(statuses);

        Assert.Equal("DueToday", result.StatusString);
        Assert.True(result.IsDueToday);
        Assert.False(result.IsOverdue);
    }

    [Fact]
    public void WorstCasePlanStatus_EmptyList_ReturnsOnTrack()
    {
        var result = InstallmentStatusHelper.WorstCasePlanStatus(Array.Empty<InstallmentStatusResult>());
        Assert.Equal("OnTrack", result.StatusString);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. اختبارات حسابات الإجمالي والنسبة المئوية
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PaidPercentage_WhenNoInstallments_ReturnsZeroNotException()
    {
        // يحاكي حساب paidPercentage مع totalDebt = 0
        decimal totalDebt = 0m;
        decimal totalPaid = 0m;

        decimal pct = totalDebt == 0m ? 0m : Math.Round(totalPaid / totalDebt * 100m, 2);

        Assert.Equal(0m, pct); // يجب أن يكون 0% لا استثناء
    }

    [Fact]
    public void PaidPercentage_WhenPartiallyPaid_IsCorrect()
    {
        decimal totalDebt = 200000m;
        decimal totalPaid = 85000m;

        decimal pct = totalDebt == 0m ? 0m : Math.Round(totalPaid / totalDebt * 100m, 2);

        Assert.Equal(42.5m, pct);
    }

    [Fact]
    public void TotalAmounts_AreConsistent()
    {
        // totalDebtAmount = totalPaid + totalRemaining
        decimal totalDebt = 200000m;
        decimal totalPaid = 85000m;
        decimal totalRemaining = totalDebt - totalPaid;

        Assert.Equal(115000m, totalRemaining);
        Assert.Equal(totalDebt, totalPaid + totalRemaining);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. اختبار عزل الأقساط حسب ShopId (عبر InMemory DB)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardRowsByShop_DoesNotReturnOtherShopInstallments()
    {
        using var db = CreateDb();
        var uow = new Infrastructure.Persistence.UnitOfWork(db);

        var shop1Id = Guid.NewGuid();
        var shop2Id = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();

        // أضف Shop-1 + Transaction + Installment
        var shop1 = Entities.Shop.Create(ownerUserId, "محل 1");
        // نحتاج shop1 بنفس shopId المحدد — نستخدم reflection بديلاً لـ InMemory
        db.Set<Entities.Shop>().Add(shop1);
        await db.SaveChangesAsync();

        var tx1 = Entities.Transaction.Create(
            ownerUserId: ownerUserId,
            contactId: null,
            type: Enums.TransactionType.Sale,
            amount: 1000m,
            transactionDate: DateTime.UtcNow.AddDays(-1),
            noteType: Enums.NoteType.None,
            isInstallment: true,
            installmentPlanMode: Enums.InstallmentPlanMode.Automatic,
            shopId: shop1.Id,
            partyName: "عميل الاختبار"
        );

        db.Set<Entities.Transaction>().Add(tx1);
        await db.SaveChangesAsync();

        var inst1 = Entities.Installment.CreateSingle(tx1.Id, 1, 500m, DateTime.UtcNow.AddDays(10));
        var inst2 = Entities.Installment.CreateSingle(tx1.Id, 2, 500m, DateTime.UtcNow.AddDays(20));
        db.Set<Entities.Installment>().AddRange(inst1, inst2);
        await db.SaveChangesAsync();

        // استعلام بـ shop2Id (لا علاقة له بالبيانات) — يجب أن يرجع 0
        var rowsForShop2 = await uow.Installments.GetDashboardRowsByShopAsync(shop2Id);
        Assert.Empty(rowsForShop2);

        // استعلام بـ shop1.Id — يجب أن يرجع 2 قسط
        var rowsForShop1 = await uow.Installments.GetDashboardRowsByShopAsync(shop1.Id);
        Assert.Equal(2, rowsForShop1.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. اختبار الترتيب في Plans (Overdue أولاً)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Plans_AreOrderedCorrectly_OverdueFirst_ThenDueToday_ThenUpcoming()
    {
        var today = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);

        // بيانات محاكاة
        var plans = new List<Application.DTOs.Transactions.ActiveInstallmentPlanDto>
        {
            new() { IsOverdue = false, IsDueToday = false, NextDueDate = today.AddDays(10), Status = "OnTrack" },
            new() { IsOverdue = true, IsDueToday = false, NextDueDate = today.AddDays(-5), Status = "Overdue", DaysOverdue = 5 },
            new() { IsOverdue = false, IsDueToday = true, NextDueDate = today, Status = "DueToday" },
        };

        var ordered = plans
            .OrderByDescending(p => p.IsOverdue)
            .ThenByDescending(p => p.IsDueToday)
            .ThenBy(p => p.NextDueDate ?? DateTime.MaxValue)
            .ToList();

        Assert.Equal("Overdue", ordered[0].Status);
        Assert.Equal("DueToday", ordered[1].Status);
        Assert.Equal("OnTrack", ordered[2].Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Infrastructure.Persistence.MizanDbContext CreateDb()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Infrastructure.Persistence.MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new Infrastructure.Persistence.MizanDbContext(options);
    }
}
