using System.Security.Cryptography;
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
    private const int MaxActiveDevices = 5;

    public AuthService(
        IUnitOfWork unitOfWork,
        IJwtProvider jwtProvider,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _jwtProvider = jwtProvider;
        _emailService = emailService;
    }

    public async Task<OtpResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        string identifier = !string.IsNullOrWhiteSpace(request.Email) 
            ? request.Email.Trim() 
            : (request.WhatsAppNumber?.Trim() ?? string.Empty);

        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new BadRequestException("البريد الإلكتروني أو رقم الهاتف مطلوب للتسجيل");
        }

        string normalizedIdentifier = identifier;
        if (!identifier.Contains('@'))
        {
            var tempUser = User.Create(identifier, request.FirstName, request.LastName);
            normalizedIdentifier = tempUser.WhatsAppNumber;
        }

        var existingUser = await _unitOfWork.Users.GetByWhatsAppNumberAsync(normalizedIdentifier, cancellationToken);
        if (existingUser == null)
        {
            var newUser = User.Create(
                normalizedIdentifier, 
                request.FirstName, 
                request.LastName
            );
            await _unitOfWork.Users.AddAsync(newUser, cancellationToken);
        }
        else
        {
            existingUser.UpdateProfile(request.FirstName, request.LastName);
            _unitOfWork.Users.Update(existingUser);
        }

        return await GenerateAndSendOtpAsync(normalizedIdentifier, $"{request.FirstName} {request.LastName}", cancellationToken);
    }

    public async Task<OtpResponse> SendOtpAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new BadRequestException("البريد الإلكتروني أو رقم الهاتف مطلوب");
        }

        string normalizedIdentifier = identifier.Trim();
        if (!normalizedIdentifier.Contains('@'))
        {
            var tempUser = User.Create(normalizedIdentifier, "مستخدم", "جديد");
            normalizedIdentifier = tempUser.WhatsAppNumber;
        }

        return await GenerateAndSendOtpAsync(normalizedIdentifier, "مستخدم", cancellationToken);
    }

    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        string identifier = request.TargetIdentifier;
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new BadRequestException("البريد الإلكتروني أو رقم الهاتف مطلوب");
        }

        string normalizedIdentifier = identifier.Trim();
        if (!normalizedIdentifier.Contains('@'))
        {
            var tempUser = User.Create(normalizedIdentifier, "مستخدم", "جديد");
            normalizedIdentifier = tempUser.WhatsAppNumber;
        }

        var otp = await _unitOfWork.OtpCodes.GetLatestValidOtpAsync(normalizedIdentifier, cancellationToken);
        if (otp == null)
        {
            throw new BadRequestException("كود التحقق غير صحيح أو منتهي الصلاحية");
        }

        bool isVerified = otp.Verify(request.Code);
        _unitOfWork.OtpCodes.Update(otp);

        if (!isVerified)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new BadRequestException("كود التحقق غير صحيح");
        }

        var user = await _unitOfWork.Users.GetByWhatsAppNumberAsync(normalizedIdentifier, cancellationToken);
        bool isNewUser = false;

        if (user == null)
        {
            user = User.Create(
                normalizedIdentifier, 
                "مستخدم", 
                "جديد"
            );
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
                token.Revoke("Exceeded maximum active devices");
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
            WhatsAppNumber = user.WhatsAppNumber,
            UserType = user.UserType,
            ShopName = shop?.ShopName
        };
    }

    public async Task<AuthResponse> SelectUserTypeAsync(int userId, SelectUserTypeRequest request, CancellationToken cancellationToken = default)
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
            WhatsAppNumber = user.WhatsAppNumber,
            UserType = user.UserType,
            ShopName = shopName
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var existingRefreshToken = await _unitOfWork.RefreshTokens.GetByTokenAsync(request.RefreshToken, cancellationToken);
        if (existingRefreshToken == null || !existingRefreshToken.IsActive)
        {
            throw new UnauthorizedException("رمز التحديث غير صالح أو منتهي الصلاحية");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(existingRefreshToken.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("الحساب غير مفعل أو غير موجود");
        }

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
            WhatsAppNumber = user.WhatsAppNumber,
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

    private async Task<OtpResponse> GenerateAndSendOtpAsync(string identifier, string recipientName, CancellationToken cancellationToken)
    {
        await _unitOfWork.OtpCodes.InvalidatePreviousOtpsAsync(identifier, cancellationToken);

        // Generate 6 digit cryptographic random number
        var randomCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var otp = OtpCode.Create(identifier, randomCode, expirySeconds: 120);

        await _unitOfWork.OtpCodes.AddAsync(otp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send via Email Service
        await _emailService.SendOtpEmailAsync(identifier, randomCode, recipientName, cancellationToken);

        var isEmail = identifier.Contains('@');

        return new OtpResponse
        {
            OtpSent = true,
            ExpiresInSeconds = 120,
            Message = isEmail ? "تم إرسال كود التحقق بنجاح إلى بريدك الإلكتروني" : "تم إرسال كود التحقق بنجاح",
            DevCode = randomCode // Exposed for testing/development
        };
    }
}
