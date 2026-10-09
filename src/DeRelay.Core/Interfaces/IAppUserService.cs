using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IAppUserService
{
    public Task<int> CreateAppUserAsync(string email, string passwordHash);
    public Task DeleteAppUserByIdAsync(int appUserId);
    /// <summary>
    /// Returns <see cref="AppUser"/> entity if not null, otherwise throw exception
    /// </summary>
    public Task<AppUser> ReturnAppUserByIdAsync(int appUserId);
    public Task<bool> CheckIfEmailAvailableInAppUser(string email);
    public Task<AppUser?> ReturnAppUserByEmail(string email);
    public string NormalizeEmail(string email);
    public Task<bool> CheckHasProfile(int appUserId);
}