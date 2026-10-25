using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.BackgroundServices;

public class ReminderTriggerWorker : BackgroundService
{
    private readonly IReminderTriggerChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderTriggerWorker> _logger;

    public ReminderTriggerWorker(
        IReminderTriggerChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ReminderTriggerWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 ReminderTriggerWorker background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var ownerUserId = await _channel.Reader.ReadAsync(stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<IReminderScanner>();

                await scanner.ScanForOwnerAsync(ownerUserId, cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error in ReminderTriggerWorker.");
            }
        }

        _logger.LogInformation("🛑 ReminderTriggerWorker background service stopped.");
    }
}
