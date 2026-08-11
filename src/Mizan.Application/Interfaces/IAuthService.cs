using Mizan.Application.DTOs.Auth;

namespace Mizan.Application.Interfaces;

public interface IAuthService
{
    Task<OtpResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<OtpResponse> SendOtpAsync(string identifier, CancellationToken cancellationToken = default);
    Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> SelectUserTypeAsync(int userId, SelectUserTypeRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
