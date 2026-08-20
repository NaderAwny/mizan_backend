using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.BackgroundServices;

public class PeriodicReportEmailWorker : BackgroundService
{
    private readonly IPeriodicReportEmailChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PeriodicReportEmailWorker> _logger;

    public PeriodicReportEmailWorker(
        IPeriodicReportEmailChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<PeriodicReportEmailWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 PeriodicReportEmailWorker background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _channel.Reader.ReadAsync(stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                bool sent = await emailService.SendPeriodicReportEmailAsync(
                    job.RecipientEmail,
                    job.RecipientName,
                    job.BatchNumber,
                    job.PdfBytes,
                    stoppingToken);

                if (sent)
                {
                    var savedReport = await unitOfWork.PeriodicReports.GetByIdAsync(job.ReportId, job.OwnerUserId, stoppingToken);
                    if (savedReport != null)
                    {
                        savedReport.MarkEmailSent();
                        unitOfWork.PeriodicReports.Update(savedReport);
                        await unitOfWork.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("✅ Periodic report email sent and marked for ReportId {ReportId}", job.ReportId);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ Failed to send periodic report email for ReportId {ReportId}. PeriodicReportEmailRetryService will retry later.", job.ReportId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error processing periodic report email job from channel.");
            }
        }

        _logger.LogInformation("🛑 PeriodicReportEmailWorker background service stopped.");
    }
}
