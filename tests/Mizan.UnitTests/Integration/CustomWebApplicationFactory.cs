using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Persistence;

namespace Mizan.UnitTests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    public static readonly FakeEmailService EmailServiceInstance = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<MizanDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IEmailService>(EmailServiceInstance);
        });
    }

    public class FakeEmailService : IEmailService
    {
        public string? LastCapturedOtp { get; set; }
        public string? LastRecipientEmail { get; set; }

        public Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
        {
            LastCapturedOtp = otpCode;
            LastRecipientEmail = toEmail;
            return Task.FromResult(true);
        }

        public Task<bool> SendInstallmentReminderEmailAsync(
            string toEmail,
            string recipientName,
            string contactName,
            decimal amount,
            DateTime dueDate,
            int daysUntilDue,
            CancellationToken cancellationToken = default)
        {
            LastRecipientEmail = toEmail;
            return Task.FromResult(true);
        }

        public Task<bool> SendPeriodicReportEmailAsync(
            string toEmail,
            string recipientName,
            int batchNumber,
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
        {
            LastRecipientEmail = toEmail;
            return Task.FromResult(true);
        }
    }
}
