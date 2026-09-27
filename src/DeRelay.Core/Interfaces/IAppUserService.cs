using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IAppUserService
{
    public Task<int> CreateAppUserAsync(string userName, string passwordHash, int personId);
    public Task DeleteAppUserByIdAsync(int appUserId);
    /// <summary>
    /// Returns <see cref="AppUser"/> entity if not null, otherwise throw exception
    /// </summary>
    public Task<AppUser> ReturnAppUserByIdAsync(int appUserId);
    public Task<bool> CheckIfUserNameAvailableInAppUser(string userName);
    public Task<AppUser?> ReturnAppUserByUsername(string userName);
}