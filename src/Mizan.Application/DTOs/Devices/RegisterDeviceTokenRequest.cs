using System.ComponentModel.DataAnnotations;
using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Devices;

public class RegisterDeviceTokenRequest
{
    [Required(ErrorMessage = "رمز الجهاز مطلوب ويجب ألا يتجاوز 512 حرفاً")]
    [StringLength(512, MinimumLength = 1, ErrorMessage = "رمز الجهاز مطلوب ويجب ألا يتجاوز 512 حرفاً")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع منصة الجهاز مطلوب")]
    public DevicePlatform Platform { get; set; }

    [StringLength(128, ErrorMessage = "معرف الجهاز يجب ألا يتجاوز 128 حرفاً")]
    public string? DeviceId { get; set; }
}
