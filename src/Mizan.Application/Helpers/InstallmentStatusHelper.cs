using Mizan.Application.DTOs.Transactions;
using Mizan.Core.Enums;

namespace Mizan.Application.Helpers;

/// <summary>
/// Helper مركزي لتحديد حالة القسط في الوقت الفعلي.
/// يُستخدم في نقطة واحدة فقط لتفادي التكرار والتعارض.
/// مصدر "اليوم الحالي" = DateTime.UtcNow.Date (متوافق مع باقي الكود).
/// </summary>
public static class InstallmentStatusHelper
{
    /// <summary>
    /// تحديد حالة قسط فردي وإرجاع التفاصيل كاملةً.
    /// </summary>
    /// <param name="isPaid">هل القسط مدفوع (Status == Paid)?</param>
    /// <param name="dueDate">تاريخ استحقاق القسط.</param>
    /// <param name="today">تاريخ اليوم (UTC) — اختياري، يُستخدم UtcNow.Date افتراضياً.</param>
    /// <returns>تفاصيل الحالة المحسوبة.</returns>
    public static InstallmentStatusResult Evaluate(bool isPaid, DateTime dueDate, DateTime? today = null)
    {
        var todayDate = (today ?? DateTime.UtcNow).Date;
        var dueDateOnly = dueDate.Date;

        if (isPaid)
        {
            return new InstallmentStatusResult
            {
                Status = InstallmentPlanStatusType.Paid,
                StatusString = "Paid",
                IsOverdue = false,
                IsDueToday = false,
                DaysOverdue = 0
            };
        }

        if (dueDateOnly < todayDate)
        {
            return new InstallmentStatusResult
            {
                Status = InstallmentPlanStatusType.Overdue,
                StatusString = "Overdue",
                IsOverdue = true,
                IsDueToday = false,
                DaysOverdue = (todayDate - dueDateOnly).Days
            };
        }

        if (dueDateOnly == todayDate)
        {
            return new InstallmentStatusResult
            {
                Status = InstallmentPlanStatusType.DueToday,
                StatusString = "DueToday",
                IsOverdue = false,
                IsDueToday = true,
                DaysOverdue = 0
            };
        }

        // dueDateOnly > todayDate
        return new InstallmentStatusResult
        {
            Status = InstallmentPlanStatusType.Upcoming,
            StatusString = "Upcoming",
            IsOverdue = false,
            IsDueToday = false,
            DaysOverdue = 0
        };
    }

    /// <summary>
    /// تحديد الحالة من كيان <see cref="Mizan.Core.Enums.InstallmentStatus"/> الموجود.
    /// </summary>
    public static InstallmentStatusResult Evaluate(InstallmentStatus status, DateTime dueDate, DateTime? today = null)
    {
        bool isPaid = status == InstallmentStatus.Paid;
        return Evaluate(isPaid, dueDate, today);
    }

    /// <summary>
    /// يحدد "أسوأ حالة" لخطة قسط كاملة من قائمة حالات أقساطها الفرعية.
    /// الترتيب (أسوأ إلى أفضل): Overdue > DueToday > Upcoming > Paid.
    /// لو كل الأقساط مدفوعة → Paid. وإلا OnTrack كحالة للخطة كلها إن لم يوجد أسوأ.
    /// </summary>
    public static InstallmentStatusResult WorstCasePlanStatus(IEnumerable<InstallmentStatusResult> itemStatuses)
    {
        InstallmentStatusResult? worst = null;

        foreach (var s in itemStatuses)
        {
            if (worst == null || IsWorseThan(s.Status, worst.Status))
                worst = s;
        }

        if (worst == null)
        {
            // لا أقساط على الإطلاق → OnTrack افتراضياً
            return new InstallmentStatusResult
            {
                Status = InstallmentPlanStatusType.OnTrack,
                StatusString = "OnTrack",
                IsOverdue = false,
                IsDueToday = false,
                DaysOverdue = 0
            };
        }

        // إذا أفضل حالة هي Paid → الخطة مكتملة
        if (worst.Status == InstallmentPlanStatusType.Paid)
            return worst;

        // إذا أسوأ حالة هي Upcoming → الخطة OnTrack
        if (worst.Status == InstallmentPlanStatusType.Upcoming)
        {
            return new InstallmentStatusResult
            {
                Status = InstallmentPlanStatusType.OnTrack,
                StatusString = "OnTrack",
                IsOverdue = false,
                IsDueToday = false,
                DaysOverdue = 0
            };
        }

        return worst;
    }

    private static bool IsWorseThan(InstallmentPlanStatusType a, InstallmentPlanStatusType b)
    {
        return Severity(a) > Severity(b);
    }

    private static int Severity(InstallmentPlanStatusType status) => status switch
    {
        InstallmentPlanStatusType.Overdue => 4,
        InstallmentPlanStatusType.DueToday => 3,
        InstallmentPlanStatusType.Upcoming => 2,
        InstallmentPlanStatusType.OnTrack => 1,
        InstallmentPlanStatusType.Paid => 0,
        _ => 0
    };
}

/// <summary>نتيجة حساب حالة قسط واحد.</summary>
public class InstallmentStatusResult
{
    public InstallmentPlanStatusType Status { get; init; }
    public string StatusString { get; init; } = string.Empty;
    public bool IsOverdue { get; init; }
    public bool IsDueToday { get; init; }
    public int DaysOverdue { get; init; }
}
