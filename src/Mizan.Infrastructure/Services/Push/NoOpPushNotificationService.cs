using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Services.Push;

public class NoOpPushNotificationService : IPushNotificationService
{
    private readonly ILogger<NoOpPushNotificationService> _logger;

    public NoOpPushNotificationService(ILogger<NoOpPushNotificationService> logger)
    {
        _logger = logger;
    }

    public Task<PushSendResult> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug("Push skipped (Firebase disabled) for user {UserId}", userId);
        return Task.FromResult(new PushSendResult(0, 0, 0));
    }

    public Task<IReadOnlyList<string>> SendDebugVariantsAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Debug push variants skipped (Firebase disabled) for user {UserId}", userId);
        IReadOnlyList<string> empty = Array.Empty<string>();
        return Task.FromResult(empty);
    }
}
