using Mizan.Application.DTOs.Devices;

namespace Mizan.Application.Interfaces;

public interface IDeviceTokenService
{
    Task RegisterAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken ct = default);
    Task UnregisterAsync(Guid userId, string token, CancellationToken ct = default);
}
