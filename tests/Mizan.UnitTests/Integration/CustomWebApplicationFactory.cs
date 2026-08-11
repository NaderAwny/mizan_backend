using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Infrastructure.Persistence;

namespace Mizan.UnitTests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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

            services.AddScoped<IEmailService, FakeEmailService>();
        });
    }

    private class FakeEmailService : IEmailService
    {
        public Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName = "", CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
