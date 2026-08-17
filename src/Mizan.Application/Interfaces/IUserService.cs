using Mizan.Application.DTOs.Users;

namespace Mizan.Application.Interfaces;

public interface IUserService
{
    Task<UserProfileResponse> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}
