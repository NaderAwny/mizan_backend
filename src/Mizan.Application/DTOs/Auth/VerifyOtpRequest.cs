using System.ComponentModel.DataAnnotations;

namespace Mizan.Application.DTOs.Auth;

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "رقم الواتساب مطلوب")]
    public string WhatsAppNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "كود التحقق مطلوب")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "كود التحقق يجب أن يتكون من 6 أرقام")]
    public string Code { get; set; } = string.Empty;
}
