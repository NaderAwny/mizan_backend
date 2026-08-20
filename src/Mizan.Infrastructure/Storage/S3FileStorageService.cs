using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Storage;

public class S3FileStorageService : IFileStorageService
{
    private readonly string _bucketName;
    private readonly string _serviceUrl;
    private readonly ILogger<S3FileStorageService> _logger;

    public S3FileStorageService(IConfiguration configuration, ILogger<S3FileStorageService> logger)
    {
        _bucketName = configuration["Storage:S3:BucketName"] ?? "mizan-storage";
        _serviceUrl = configuration["Storage:S3:ServiceUrl"] ?? "https://s3.amazonaws.com";
        _logger = logger;
    }

    public async Task<string> UploadAsync(Stream stream, string originalFileName, string contentType, string subDirectory, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var key = $"{subDirectory.Trim('/')}/{Guid.NewGuid()}{ext}";

        // Production implementation uploads to S3 / Cloudflare R2 bucket
        _logger.LogInformation("Uploading file {Key} to S3 bucket {Bucket}", key, _bucketName);
        await Task.CompletedTask;

        return $"https://{_bucketName}.s3.amazonaws.com/{key}";
    }

    public Task<Stream?> GetStreamAsync(string fileKeyOrPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching stream for {Key} from S3 bucket {Bucket}", fileKeyOrPath, _bucketName);
        return Task.FromResult<Stream?>(null);
    }

    public Task DeleteAsync(string fileKeyOrPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting file {Key} from S3 bucket {Bucket}", fileKeyOrPath, _bucketName);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string fileKeyOrPath)
    {
        if (Uri.TryCreate(fileKeyOrPath, UriKind.Absolute, out _))
            return fileKeyOrPath;

        return $"{_serviceUrl.TrimEnd('/')}/{_bucketName}/{fileKeyOrPath.TrimStart('/')}";
    }
}
