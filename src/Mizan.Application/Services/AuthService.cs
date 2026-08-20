using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtProvider _jwtProvider;
    private readonly IEmailService _emailService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;
    private const int MaxActiveDevices = 5;

    public AuthService(
        IUnitOfWork unitOfWork,
        IJwtProvider jwtProvider,
        IEmailService emailService,
        IHostEnvironment environment,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _jwtProvider = jwtProvider;
        _emailService = emailService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<OtpResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);
        if (existingUser == null)
        {
            // New user: create record now so OTP is associated with an account
            var newUser = User.Create(email, request.FirstName, request.LastName);
            await _unitOfWork.Users.AddAsync(newUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        // H2 (Option A): If user already exists, do NOT update their profile.
        // Only send a new OTP. This prevents an attacker from changing another
        // user's name simply by calling /register with their email.

        return await GenerateAndSendOtpAsync(email, cancellationToken);
    }

    public async Task<OtpResponse> SendOtpAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new BadRequestException("البريد الإلكتروني مطلوب");

        var email = identifier.Trim().ToLowerInvariant();
        return await GenerateAndSendOtpAsync(email, cancellationToken);
    }

    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            throw new BadRequestException("Invalid or expired verification code");

        var otp = await _unitOfWork.OtpCodes.GetLatestValidOtpAsync(email, cancellationToken);
        if (otp == null)
            throw new BadRequestException("Invalid or expired verification code");

        bool isVerified = otp.Verify(request.Code);
        _unitOfWork.OtpCodes.Update(otp);

        if (!isVerified)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("Invalid or expired verification code");
        }

        var user = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);
        bool isNewUser = false;

        if (user == null)
        {
            user = User.Create(email, "مستخدم", "جديد");
            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            isNewUser = true;
        }

        // Handle Active Devices / Refresh Tokens limit (Max 5)
        var activeTokens = await _unitOfWork.RefreshTokens.GetActiveTokensByUserIdAsync(user.Id, cancellationToken);
        if (activeTokens.Count >= MaxActiveDevices)
        {
            var tokensToRevoke = activeTokens
                .OrderBy(t => t.CreatedAt)
                .Take(activeTokens.Count - MaxActiveDevices + 1);

            foreach (var token in tokensToRevoke)
            {
                token.Revoke();
                _unitOfWork.RefreshTokens.Update(token);
            }
        }

        var accessToken = _jwtProvider.GenerateAccessToken(user);
        var refreshTokenString = _jwtProvider.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            user.Id,
            refreshTokenString,
            DateTime.UtcNow.AddDays(_jwtProvider.RefreshTokenExpirationDays)
        );

        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var shop = await _unitOfWork.Shops.GetByOwnerIdAsync(user.Id, cancellationToken);

        return new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresInSeconds = _jwtProvider.AccessTokenExpirationSeconds,
            IsNewUser = isNewUser,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserType = user.UserType,
            ShopName = shop?.ShopName
        };
    }

    public async Task<AuthResponse> SelectUserTypeAsync(Guid userId, SelectUserTypeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new NotFoundException("المستخدم", userId);

        user.SetUserType(request.UserType);
        _unitOfWork.Users.Update(user);

        string? shopName = null;
        if (request.UserType.ToLowerInvariant() == "shop_owner")
        {
            if (string.IsNullOrWhiteSpace(request.ShopName))
                throw new BadRequestException("اسم المحل مطلوب لأصحاب المحلات");

            var shop = await _unitOfWork.Shops.GetByOwnerIdAsync(userId, cancellationToken);
            if (shop == null)
            {
                shop = Shop.Create(userId, request.ShopName, request.Address ?? string.Empty);
                await _unitOfWork.Shops.AddAsync(shop, cancellationToken);
            }
            else
            {
                shop.Update(request.ShopName, request.Address ?? string.Empty);
                _unitOfWork.Shops.Update(shop);
            }
            shopName = shop.ShopName;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtProvider.GenerateAccessToken(user);
        var refreshTokenString = _jwtProvider.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            user.Id,
            refreshTokenString,
            DateTime.UtcNow.AddDays(_jwtProvider.RefreshTokenExpirationDays)
        );

        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresInSeconds = _jwtProvider.AccessTokenExpirationSeconds,
            IsNewUser = false,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserType = user.UserType,
            ShopName = shopName
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var existingRefreshToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(request.RefreshToken, cancellationToken);
        if (existingRefreshToken == null || !existingRefreshToken.IsActive)
            throw new UnauthorizedException("رمز التحديث غير صالح أو منتهي الصلاحية");

        var user = await _unitOfWork.Users.GetByIdAsync(existingRefreshToken.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            throw new UnauthorizedException("الحساب غير مفعل أو غير موجود");

        var newAccessToken = _jwtProvider.GenerateAccessToken(user);
        var newRefreshTokenString = _jwtProvider.GenerateRefreshToken();

        existingRefreshToken.Revoke(newRefreshTokenString);
        _unitOfWork.RefreshTokens.Update(existingRefreshToken);

        var newRefreshToken = RefreshToken.Create(
            user.Id,
            newRefreshTokenString,
            DateTime.UtcNow.AddDays(_jwtProvider.RefreshTokenExpirationDays)
        );

        await _unitOfWork.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var shop = await _unitOfWork.Shops.GetByOwnerIdAsync(user.Id, cancellationToken);

        return new AuthResponse
        {
            Token = newAccessToken,
            RefreshToken = newRefreshTokenString,
            ExpiresInSeconds = _jwtProvider.AccessTokenExpirationSeconds,
            IsNewUser = false,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserType = user.UserType,
            ShopName = shop?.ShopName
        };
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var token = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshToken, cancellationToken);
        if (token != null && token.IsActive)
        {
            token.Revoke();
            _unitOfWork.RefreshTokens.Update(token);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<OtpResponse> GenerateAndSendOtpAsync(string email, CancellationToken cancellationToken)
    {
        await _unitOfWork.OtpCodes.InvalidatePreviousOtpsAsync(email, cancellationToken);

        // Generate 6-digit cryptographic random number
        // GetInt32 upper bound is exclusive, so 1000000 gives us 100000..999999
        var randomCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otp = OtpCode.Create(email, randomCode, expirySeconds: 120);

        await _unitOfWork.OtpCodes.AddAsync(otp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _emailService.SendOtpEmailAsync(email, randomCode, cancellationToken);

        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("🔐 [DEV SERVER-ONLY OTP] Code for {Email} is: {Code}", email, randomCode);
        }

        return new OtpResponse
        {
            OtpSent = true,
            ExpiresInSeconds = 120,
            Message = "تم إرسال كود التحقق بنجاح إلى بريدك الإلكتروني"
        };
    }
}
