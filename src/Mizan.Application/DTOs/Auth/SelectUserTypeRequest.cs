using System.ComponentModel.DataAnnotations;

namespace Mizan.Application.DTOs.Auth;

public class SelectUserTypeRequest
{
    [Required(ErrorMessage = "نوع الحساب مطلوب")]
    [RegularExpression("^(customer|shop_owner)$", ErrorMessage = "نوع الحساب يجب أن يكون customer أو shop_owner")]
    public string UserType { get; set; } = "customer";

    [MaxLength(100, ErrorMessage = "اسم المحل يجب ألا يتجاوز 100 حرف")]
    public string? ShopName { get; set; }

    [MaxLength(200, ErrorMessage = "عنوان المحل يجب ألا يتجاوز 200 حرف")]
    public string? Address { get; set; }
}
