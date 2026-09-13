using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Persistence;

namespace Mizan.UnitTests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    public FakeEmailService EmailService { get; } = new();
    public FakeEmailVerificationService EmailVerificationService { get; } = new();

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
            var testSqlConn = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(testSqlConn))
            {
                var connBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(testSqlConn);
                connBuilder.InitialCatalog = $"{connBuilder.InitialCatalog}_{_dbName.Replace("-", "")}";

                services.AddDbContext<MizanDbContext>(options =>
                {
                    options.UseSqlServer(connBuilder.ConnectionString);
                });

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
                db.Database.EnsureCreated();
            }
            else
            {
                services.AddDbContext<MizanDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_dbName);
                });
            }

            // Replace IEmailService with test fake
            var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null)
                services.Remove(emailDescriptor);

            services.AddSingleton<IEmailService>(EmailService);

            // Replace IEmailVerificationService with test fake (avoids real DNS/SMTP in tests)
            var verifyDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailVerificationService));
            if (verifyDescriptor != null)
                services.Remove(verifyDescriptor);

            services.AddSingleton<IEmailVerificationService>(EmailVerificationService);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var testSqlConn = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(testSqlConn))
            {
                try
                {
                    using var scope = Services.CreateScope();
                    var db = scope.ServiceProvider.GetService<MizanDbContext>();
                    db?.Database.EnsureDeleted();
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
        base.Dispose(disposing);
    }

    // -----------------------------------------------------------------------
    // Fakes
    // -----------------------------------------------------------------------

    public class FakeEmailService : IEmailService
    {
        public bool ShouldSucceed { get; set; } = true;
        public string? LastCapturedOtp { get; set; }
        public string? LastRecipientEmail { get; set; }

        public Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
        {
            if (!ShouldSucceed)
                return Task.FromResult(false);

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

        public Task<bool> SendInstallmentReminderToContactEmailAsync(
            string toEmail,
            string contactName,
            string shopOwnerName,
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

    public class FakeEmailVerificationService : IEmailVerificationService
    {
        /// <summary>
        /// Controls the verdict returned by VerifyAsync. Default: deliverable (passes all tests).
        /// Override per-test to simulate disposable/non-existent mailbox scenarios.
        /// </summary>
        public EmailVerificationResult NextResult { get; set; } = EmailVerificationResult.Valid("TestVerified");

        public Task<EmailVerificationResult> VerifyAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(NextResult);
    }
}
