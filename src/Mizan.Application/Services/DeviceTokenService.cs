using Microsoft.Extensions.Logging;
using Mizan.Application.DTOs.Devices;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class DeviceTokenService : IDeviceTokenService
{
    private const int MaxTokensPerUser = 10;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeviceTokenService> _logger;

    public DeviceTokenService(IUnitOfWork unitOfWork, ILogger<DeviceTokenService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task RegisterAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token) || request.Token.Trim().Length > 512)
        {
            throw new BadRequestException("رمز الجهاز مطلوب ويجب ألا يتجاوز 512 حرفاً");
        }

        var token = request.Token.Trim();
        var existing = await _unitOfWork.DeviceTokens.GetByTokenAsync(token, ct);
        var userTokens = await _unitOfWork.DeviceTokens.GetByUserIdAsync(userId, ct);

        if (existing != null)
        {
            if (existing.UserId != userId)
            {
                _logger.LogInformation("Reassigning device token from user {OldUserId} to {NewUserId}", existing.UserId, userId);
                existing.Reassign(userId, request.Platform, request.DeviceId);
            }
            else
            {
                existing.Touch(request.Platform, request.DeviceId);
            }

            var all = userTokens.Where(t => t.Id != existing.Id).Concat(new[] { existing }).ToList();
            if (all.Count > MaxTokensPerUser)
            {
                var toRemove = all.OrderBy(t => t.LastSeenAt).Take(all.Count - MaxTokensPerUser).ToList();
                _unitOfWork.DeviceTokens.RemoveRange(toRemove);
            }
        }
        else
        {
            if (userTokens.Count >= MaxTokensPerUser)
            {
                var excess = userTokens.Count - (MaxTokensPerUser - 1);
                var toRemove = userTokens.OrderBy(t => t.LastSeenAt).Take(excess).ToList();
                _unitOfWork.DeviceTokens.RemoveRange(toRemove);
            }

            var newToken = DeviceToken.Create(userId, token, request.Platform, request.DeviceId);
            await _unitOfWork.DeviceTokens.AddAsync(newToken, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UnregisterAsync(Guid userId, string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var trimmedToken = token.Trim();
        var existing = await _unitOfWork.DeviceTokens.GetByTokenAsync(trimmedToken, ct);

        if (existing != null && existing.UserId == userId)
        {
            _unitOfWork.DeviceTokens.Remove(existing);
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogInformation("Unregistered device token for user {UserId}", userId);
        }
    }
}
