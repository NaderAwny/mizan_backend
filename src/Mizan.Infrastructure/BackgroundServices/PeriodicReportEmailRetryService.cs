using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Reports;
using Mizan.Application.Interfaces;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.BackgroundServices;

public class PeriodicReportEmailRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<PeriodicReportsOptions> _optionsMonitor;
    private readonly ILogger<PeriodicReportEmailRetryService> _logger;

    public PeriodicReportEmailRetryService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<PeriodicReportsOptions> optionsMonitor,
        ILogger<PeriodicReportEmailRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 PeriodicReportEmailRetryService background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;

            if (options.Enabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    var unsentReports = await unitOfWork.PeriodicReports.GetUnsentAsync(stoppingToken);

                    if (unsentReports.Count > 0)
                    {
                        _logger.LogInformation("🔄 Found {Count} periodic reports with pending email delivery. Retrying...", unsentReports.Count);

                        int successCount = 0;
                        foreach (var report in unsentReports)
                        {
                            if (stoppingToken.IsCancellationRequested)
                                break;

                            var owner = report.Owner ?? await unitOfWork.Users.GetByIdAsync(report.OwnerUserId, stoppingToken);
                            if (owner == null || string.IsNullOrWhiteSpace(owner.Email))
                            {
                                _logger.LogWarning("⚠️ Skipping email retry for Report #{ReportId}: Owner user or email not found.", report.Id);
                                continue;
                            }

                            if (!File.Exists(report.PdfStoragePath))
                            {
                                _logger.LogError("❌ Skipping email retry for Report #{ReportId}: PDF file not found at path {Path}", report.Id, report.PdfStoragePath);
                                continue;
                            }

                            byte[] pdfBytes = await File.ReadAllBytesAsync(report.PdfStoragePath, stoppingToken);
                            string recipientName = $"{owner.FirstName} {owner.LastName}".Trim();

                            bool sent = await emailService.SendPeriodicReportEmailAsync(
                                owner.Email,
                                recipientName,
                                report.BatchNumber,
                                pdfBytes,
                                stoppingToken);

                            if (sent)
                            {
                                report.MarkEmailSent();
                                unitOfWork.PeriodicReports.Update(report);
                                await unitOfWork.SaveChangesAsync(stoppingToken);
                                successCount++;
                            }
                        }

                        if (successCount > 0)
                        {
                            _logger.LogInformation("✅ Successfully retried and delivered {Count} periodic report emails.", successCount);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ An error occurred during periodic report email retry execution.");
                }
            }
            else
            {
                _logger.LogDebug("ℹ️ Periodic reports background service is disabled in configuration.");
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

        _logger.LogInformation("🛑 PeriodicReportEmailRetryService background service stopped.");
    }
}
