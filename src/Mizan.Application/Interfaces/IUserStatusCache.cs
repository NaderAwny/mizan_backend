namespace Mizan.Application.Interfaces;

public interface IUserStatusCache
{
    Task<bool> GetOrSetUserActiveStatusAsync(Guid userId, Func<Guid, Task<bool>> statusFactory, TimeSpan? expiration = null);
    void InvalidateUserStatus(Guid userId);
}
