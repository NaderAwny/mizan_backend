using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RegisterAsync(request, cancellationToken);
        return Success(response, "تم إرسال كود التحقق بنجاح");
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] string whatsappNumber, CancellationToken cancellationToken)
    {
        var response = await _authService.SendOtpAsync(whatsappNumber, cancellationToken);
        return Success(response, "تم إرسال كود التحقق بنجاح");
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.VerifyOtpAsync(request, cancellationToken);
        return Success(response, "تم تسجيل الدخول بنجاح");
    }

    [Authorize]
    [HttpPost("select-user-type")]
    public async Task<IActionResult> SelectUserType([FromBody] SelectUserTypeRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.SelectUserTypeAsync(CurrentUserId, request, cancellationToken);
        return Success(response, "تم تحديث نوع الحساب بنجاح");
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Success(response, "تم تجديد الرمز بنجاح");
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] string refreshToken, CancellationToken cancellationToken)
    {
        await _authService.RevokeTokenAsync(refreshToken, cancellationToken);
        return Success(null, "تم تسجيل الخروج بنجاح");
    }
}
