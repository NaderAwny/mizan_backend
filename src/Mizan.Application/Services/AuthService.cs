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
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;
    private const int MaxActiveDevices = 5;

    public AuthService(
        IUnitOfWork unitOfWork,
        IJwtProvider jwtProvider,
        IEmailService emailService,
        IEmailVerificationService emailVerificationService,
        IHostEnvironment environment,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _jwtProvider = jwtProvider;
        _emailService = emailService;
        _emailVerificationService = emailVerificationService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<OtpResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // 1. Verify email deliverability and reject fake / disposable domains
        var verification = await _emailVerificationService.VerifyAsync(email, cancellationToken);
        if (!verification.IsDeliverable)
        {
            throw new BadRequestException("البريد الإلكتروني غير قادر على استقبال الرسائل أو غير موجود، من فضلك تأكد من كتابته بشكل صحيح.");
        }

        var existingUser = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);
        if (existingUser == null)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName))
                throw new BadRequestException("الاسم الأول مطلوب");

            if (string.IsNullOrWhiteSpace(request.LastName))
                throw new BadRequestException("الاسم الأخير مطلوب");

            // New user: created as inactive (unverified) until OTP is confirmed in verify-otp
            var newUser = User.Create(email, request.FirstName, request.LastName);
            newUser.Deactivate();
            await _unitOfWork.Users.AddAsync(newUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (!existingUser.IsActive)
        {
            // User previously initiated registration but never verified OTP.
            // Allow updating profile if provided, and resend OTP.
            if (!string.IsNullOrWhiteSpace(request.FirstName) && !string.IsNullOrWhiteSpace(request.LastName))
            {
                existingUser.UpdateProfile(request.FirstName, request.LastName);
                _unitOfWork.Users.Update(existingUser);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        // If user is already active/verified: do NOT update their profile.
        // Only send a new OTP. This prevents changing another user's name via register.

        return await GenerateAndSendOtpAsync(email, cancellationToken);
    }

    public async Task<OtpResponse> SendOtpAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new BadRequestException("البريد الإلكتروني مطلوب");

        var email = identifier.Trim().ToLowerInvariant();

        var user = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);
        if (user == null || !user.IsActive)
        {
            throw new NotFoundException("لا يوجد حساب مسجل بهذا البريد الإلكتروني. يرجى إنشاء حساب جديد أولًا.");
        }

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
        else if (!user.IsActive)
        {
            user.Activate();
            _unitOfWork.Users.Update(user);
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

        var emailSent = await _emailService.SendOtpEmailAsync(email, randomCode, cancellationToken);
        if (!emailSent)
        {
            throw new BadRequestException("فشل إرسال كود التحقق إلى بريدك الإلكتروني، يرجى المحاولة مرة أخرى لاحقًا.");
        }

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
