namespace Mizan.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream stream, string originalFileName, string contentType, string subDirectory, CancellationToken cancellationToken = default);
    Task<Stream?> GetStreamAsync(string fileKeyOrPath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string fileKeyOrPath, CancellationToken cancellationToken = default);
    string GetPublicUrl(string fileKeyOrPath);
}
