using System.ComponentModel.DataAnnotations;

namespace Mizan.Application.DTOs.Contacts;

public class CreateContactRequest
{
    [Required(ErrorMessage = "اسم الطرف مطلوب")]
    [MaxLength(100, ErrorMessage = "اسم الطرف يجب ألا يتجاوز 100 حرف")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "رقم الهاتف يجب ألا يتجاوز 20 حرفاً")]
    public string? PhoneNumber { get; set; }

    [MaxLength(500, ErrorMessage = "الملاحظات يجب ألا تتجاوز 500 حرف")]
    public string? Notes { get; set; }
}
