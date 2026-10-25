using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Services.Push;

public class FcmPushNotificationService : IPushNotificationService
{
    private static readonly object InitLock = new();
    private static bool _initialized;

    private readonly IUnitOfWork _unitOfWork;
    private readonly FirebaseOptions _options;
    private readonly ILogger<FcmPushNotificationService> _logger;

    public FcmPushNotificationService(
        IUnitOfWork unitOfWork,
        IOptions<FirebaseOptions> options,
        ILogger<FcmPushNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;

        EnsureFirebaseInitialized();
    }

    private void EnsureFirebaseInitialized()
    {
        if (_initialized || FirebaseApp.DefaultInstance != null)
        {
            _initialized = true;
            return;
        }

        lock (InitLock)
        {
            if (_initialized || FirebaseApp.DefaultInstance != null)
            {
                _initialized = true;
                return;
            }

            if (FirebaseCredentialHelper.TryInitializeFirebase(_options, _logger, out var projectId, out var failureReason))
            {
                _initialized = true;
                _logger.LogInformation("FirebaseApp initialized successfully for project {ProjectId}", projectId);
            }
            else
            {
                _logger.LogWarning("FirebaseApp initialization skipped: {Reason}. Push notifications will be unavailable.", failureReason);
            }
        }
    }

    public async Task<PushSendResult> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct = default)
    {
        var tokens = await _unitOfWork.DeviceTokens.GetByUserIdAsync(userId, ct);
        _logger.LogInformation("Push send started for user {UserId}: found {TokenCount} device token(s)", userId, tokens.Count);
        if (tokens.Count == 0)
        {
            return new PushSendResult(0, 0, 0);
        }

        if (FirebaseApp.DefaultInstance == null)
        {
            _logger.LogWarning("FirebaseApp is not initialized. Cannot send push to user {UserId}", userId);
            return new PushSendResult(tokens.Count, 0, tokens.Count);
        }

        try
        {
#pragma warning disable CS0618
            var fcmMessages = tokens.Select(t => new Message
            {
                Token = t.Token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = message.Title,
                    Body = message.Body
                },
                Data = message.Data != null ? new Dictionary<string, string>(message.Data) : null,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ChannelId = !string.IsNullOrWhiteSpace(_options.AndroidChannelId) ? _options.AndroidChannelId : "mizan_reminders"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default"
                    }
                }
            }).ToList();
#pragma warning restore CS0618

            var messaging = FirebaseMessaging.DefaultInstance;
            var response = await messaging.SendEachAsync(fcmMessages, ct);

            var deadTokens = new List<string>();
            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                var token = tokens[i].Token;
                var maskedToken = token.Length > 6 ? token[^6..] : token;

                if (!r.IsSuccess)
                {
                    _logger.LogWarning("Failed to send push to token ending in ...{MaskedToken}: {Error}",
                        maskedToken, r.Exception?.Message);

                    if (r.Exception is FirebaseMessagingException fme &&
                        (fme.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                         fme.MessagingErrorCode == MessagingErrorCode.SenderIdMismatch))
                    {
                        deadTokens.Add(token);
                    }
                }
            }

            if (deadTokens.Count > 0)
            {
                foreach (var deadToken in deadTokens)
                {
                    await _unitOfWork.DeviceTokens.DeleteByTokenAsync(deadToken, ct);
                    var deadMasked = deadToken.Length > 6 ? deadToken[^6..] : deadToken;
                    _logger.LogInformation("Removed inactive device token ending in ...{MaskedToken} (Unregistered/SenderIdMismatch)", deadMasked);
                }

                await _unitOfWork.SaveChangesAsync(ct);
                _logger.LogInformation("Cleaned up {Count} inactive device token(s) (Unregistered/SenderIdMismatch) for user {UserId}", deadTokens.Count, userId);
            }

            var sent = response.Responses.Count(r => r.IsSuccess);
            var failed = response.Responses.Count - sent;
            _logger.LogInformation("Push send completed for user {UserId}: {Total} found, {Sent} sent, {Failed} failed", userId, tokens.Count, sent, failed);
            return new PushSendResult(tokens.Count, sent, failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send FCM push messages for user {UserId}", userId);
            return new PushSendResult(tokens.Count, 0, tokens.Count);
        }
    }

    public async Task<IReadOnlyList<string>> SendDebugVariantsAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _unitOfWork.DeviceTokens.GetByUserIdAsync(userId, ct);
        _logger.LogInformation("Debug push variants started for user {UserId}: found {TokenCount} device token(s)", userId, tokens.Count);
        if (tokens.Count == 0)
        {
            return new[] { $"No device tokens found for user {userId}" };
        }

        if (FirebaseApp.DefaultInstance == null)
        {
            _logger.LogWarning("FirebaseApp is not initialized. Cannot send debug push to user {UserId}", userId);
            return new[] { "FirebaseApp is not initialized" };
        }

        var messaging = FirebaseMessaging.DefaultInstance;
        var summary = new List<string>();
        var channelId = !string.IsNullOrWhiteSpace(_options.AndroidChannelId) ? _options.AndroidChannelId : "mizan_reminders";

#pragma warning disable CS0618
        var variants = new (string Letter, Func<string, Message> Factory)[]
        {
            ("A", token => new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = "A",
                    Body = "test"
                }
            }),
            ("B", token => new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = "B",
                    Body = "test"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High
                }
            }),
            ("C", token => new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = "C",
                    Body = "test"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = channelId
                    }
                }
            }),
            ("D", token => new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = "D",
                    Body = "test"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = channelId,
                        Sound = "default"
                    }
                }
            }),
            ("E", token => new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = "E",
                    Body = "test"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = channelId,
                        Sound = "default",
                        Tag = "debug-e"
                    }
                }
            }),
            ("F", token => new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = "F",
                    Body = "test"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = channelId,
                        Sound = "default",
                        Tag = "debug-e"
                    }
                }
            }),
            ("G", token => new Message
            {
                Token = token,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = "G",
                    Body = "test"
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = channelId,
                        Sound = "default",
                        Tag = "debug-e"
                    }
                },
                Data = new Dictionary<string, string>
                {
                    ["type"] = "InstallmentReminder",
                    ["notificationId"] = Guid.NewGuid().ToString(),
                    ["transactionId"] = Guid.NewGuid().ToString(),
                    ["installmentId"] = Guid.NewGuid().ToString(),
                    ["screen"] = "notifications"
                }
            })
        };
#pragma warning restore CS0618

        for (int i = 0; i < variants.Length; i++)
        {
            var (letter, factory) = variants[i];
            var fcmMessages = tokens.Select(t => factory(t.Token)).ToList();

            _logger.LogInformation("FCM Debug: Sending Variant {Variant} to {Count} device(s) for user {UserId}",
                letter, fcmMessages.Count, userId);

            try
            {
                var response = await messaging.SendEachAsync(fcmMessages, ct);
                var sent = response.Responses.Count(r => r.IsSuccess);
                var failed = response.Responses.Count - sent;

                _logger.LogInformation("FCM Debug: Variant {Variant} send completed: {Sent} sent, {Failed} failed",
                    letter, sent, failed);

                for (int j = 0; j < response.Responses.Count; j++)
                {
                    var r = response.Responses[j];
                    var token = tokens[j].Token;
                    var masked = token.Length > 6 ? token[^6..] : token;

                    if (r.IsSuccess)
                    {
                        _logger.LogInformation("FCM Debug: Variant {Variant} delivered to ...{MaskedToken}, MessageId={MessageId}",
                            letter, masked, r.MessageId);
                    }
                    else
                    {
                        _logger.LogWarning("FCM Debug: Variant {Variant} failed for ...{MaskedToken}: {Error}",
                            letter, masked, r.Exception?.Message);
                    }
                }

                summary.Add($"Variant {letter}: {sent} sent, {failed} failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FCM Debug: Variant {Variant} failed with exception for user {UserId}", letter, userId);
                summary.Add($"Variant {letter}: Error - {ex.Message}");
            }

            if (i < variants.Length - 1)
            {
                await Task.Delay(1000, ct);
            }
        }

        return summary;
    }
}
