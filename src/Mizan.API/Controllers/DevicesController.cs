using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.DTOs.Devices;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

[Authorize]
[EnableRateLimiting("GeneralPolicy")]
[ApiController]
[Route("api/[controller]")]
[Route("api/v{version:apiVersion}/[controller]")]
public class DevicesController : BaseController
{
    private readonly IDeviceTokenService _deviceTokenService;

    public DevicesController(IDeviceTokenService deviceTokenService)
    {
        _deviceTokenService = deviceTokenService;
    }

    /// <summary>POST /api/devices/register — تسجيل أو تحديث رمز جهاز للإشعارات الفورية</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        await _deviceTokenService.RegisterAsync(CurrentUserId, request, cancellationToken);
        return Success(null, "تم تسجيل الجهاز بنجاح");
    }

    /// <summary>POST /api/devices/unregister — إلغاء تسجيل رمز جهاز</summary>
    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister([FromBody] UnregisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        await _deviceTokenService.UnregisterAsync(CurrentUserId, request.Token, cancellationToken);
        return Success(null, "تم إلغاء تسجيل الجهاز بنجاح");
    }
}
