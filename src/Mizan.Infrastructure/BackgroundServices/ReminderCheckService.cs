using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Notifications;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.BackgroundServices;

public class ReminderCheckService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RemindersOptions> _optionsMonitor;
    private readonly ILogger<ReminderCheckService> _logger;

    public ReminderCheckService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RemindersOptions> optionsMonitor,
        ILogger<ReminderCheckService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 ReminderCheckService background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;

            if (options.Enabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var scanner = scope.ServiceProvider.GetRequiredService<IReminderScanner>();
                    int processedCount = await scanner.ScanAndProcessRemindersAsync(null, stoppingToken);

                    if (processedCount > 0)
                    {
                        _logger.LogInformation("✅ Reminder check run completed. Sent {Count} reminders.", processedCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ An error occurred during reminder background check execution.");
                }
            }
            else
            {
                _logger.LogDebug("ℹ️ Reminders background service is disabled in configuration.");
            }

            int intervalMinutes = Math.Max(1, options.CheckIntervalMinutes);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("🛑 ReminderCheckService background service stopped.");
    }
}
