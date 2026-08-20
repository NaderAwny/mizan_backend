using Microsoft.Extensions.Hosting;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IHostEnvironment _env;

    public LocalFileStorageService(IHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> UploadAsync(Stream stream, string originalFileName, string contentType, string subDirectory, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid()}{ext}";
        var rootDir = Path.Combine(_env.ContentRootPath, "wwwroot");
        var folderPath = Path.Combine(rootDir, subDirectory);

        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, fileName);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream, cancellationToken);

        var normalizedSubDir = subDirectory.Trim('/').Replace('\\', '/');
        return $"/{normalizedSubDir}/{fileName}";
    }

    public Task<Stream?> GetStreamAsync(string fileKeyOrPath, CancellationToken cancellationToken = default)
    {
        var rootDir = Path.Combine(_env.ContentRootPath, "wwwroot");
        var relative = fileKeyOrPath.TrimStart('/', '\\');
        var fullPath = Path.Combine(rootDir, relative);

        if (!File.Exists(fullPath))
        {
            // Also check root content path fallback
            fullPath = Path.Combine(_env.ContentRootPath, relative);
            if (!File.Exists(fullPath))
                return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string fileKeyOrPath, CancellationToken cancellationToken = default)
    {
        var rootDir = Path.Combine(_env.ContentRootPath, "wwwroot");
        var relative = fileKeyOrPath.TrimStart('/', '\\');
        var fullPath = Path.Combine(rootDir, relative);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else
        {
            var fallback = Path.Combine(_env.ContentRootPath, relative);
            if (File.Exists(fallback))
            {
                File.Delete(fallback);
            }
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string fileKeyOrPath)
    {
        return fileKeyOrPath.StartsWith("/") ? fileKeyOrPath : $"/{fileKeyOrPath}";
    }
}
