using Mizan.Application.DTOs.Users;
using Mizan.Application.Interfaces;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserProfileResponse> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetWithShopAsync(userId, cancellationToken);
        if (user == null)
            throw new NotFoundException("المستخدم", userId);

        return new UserProfileResponse
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            UserType = user.UserType,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Shop = user.Shop != null ? new ShopDto
            {
                Id = user.Shop.Id,
                ShopName = user.Shop.ShopName,
                Address = user.Shop.Address,
                CreatedAt = user.Shop.CreatedAt
            } : null
        };
    }
}
