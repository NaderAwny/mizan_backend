using System.ComponentModel.DataAnnotations;

namespace Mizan.Application.DTOs.Devices;

public class UnregisterDeviceTokenRequest
{
    [Required(ErrorMessage = "رمز الجهاز مطلوب ويجب ألا يتجاوز 512 حرفاً")]
    [StringLength(512, MinimumLength = 1, ErrorMessage = "رمز الجهاز مطلوب ويجب ألا يتجاوز 512 حرفاً")]
    public string Token { get; set; } = string.Empty;
}
