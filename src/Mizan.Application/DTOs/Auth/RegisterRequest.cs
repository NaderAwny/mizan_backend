using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Mizan.Application.DTOs.Auth;

public class RegisterRequest : IValidatableObject
{
    [Required(ErrorMessage = "الاسم الأول مطلوب")]
    [MaxLength(50, ErrorMessage = "الاسم الأول يجب ألا يتجاوز 50 حرف")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "الاسم الأخير مطلوب")]
    [MaxLength(50, ErrorMessage = "الاسم الأخير يجب ألا يتجاوز 50 حرف\n")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صالحة")]
    [MaxLength(100, ErrorMessage = "البريد الإلكتروني لا يمكن أن يتجاوز 100 حرف")]
    public string Email { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Name: only Arabic or English letters and spaces
        var nameRegex = new Regex(@"^[\u0600-\u06FFa-zA-Z\s]+$");

        if (!string.IsNullOrWhiteSpace(FirstName) && !nameRegex.IsMatch(FirstName.Trim()))
            yield return new ValidationResult(
                "الاسم الأول يجب أن يحتوي على أحرف فقط (عربي أو إنجليزي)، بدون أرقام أو رموز",
                new[] { nameof(FirstName) });

        if (!string.IsNullOrWhiteSpace(LastName) && !nameRegex.IsMatch(LastName.Trim()))
            yield return new ValidationResult(
                "الاسم الأخير يجب أن يحتوي على أحرف فقط (عربي أو إنجليزي)، بدون أرقام أو رموز",
                new[] { nameof(LastName) });
    }
}
