using System.ComponentModel.DataAnnotations;

namespace Mizan.Application.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "الاسم الأول مطلوب")]
    [MaxLength(50, ErrorMessage = "الاسم الأول يجب ألا يتجاوز 50 حرف")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "الاسم الأخير مطلوب")]
    [MaxLength(50, ErrorMessage = "الاسم الأخير يجب ألا يتجاوز 50 حرف")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "رقم الواتساب غير صالح")]
    public string? WhatsAppNumber { get; set; }

    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صالحة")]
    public string? Email { get; set; }
}
