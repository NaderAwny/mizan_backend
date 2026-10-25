namespace Mizan.Application.DTOs.Transactions;

/// <summary>
/// حالة خطة القسط / القسط الفردي المُحسَبة في الوقت الفعلي (غير مخزَّنة).
/// القيم متوافقة مع منطق البند 3 في مستند الميزة.
/// </summary>
public enum InstallmentPlanStatusType
{
    Paid = 0,
    Upcoming = 1,
    DueToday = 2,
    Overdue = 3,
    OnTrack = 4      // يُستخدم فقط على مستوى الخطة الكاملة حين لا يوجد أي قسط متأخر أو مستحق اليوم
}
