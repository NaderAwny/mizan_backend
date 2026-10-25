namespace Mizan.Application.Interfaces;

public record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data, string? Tag = null);
public record PushSendResult(int TokensFound, int Sent, int Failed);

public interface IPushNotificationService
{
    Task<PushSendResult> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<string>> SendDebugVariantsAsync(Guid userId, CancellationToken ct = default);
}
