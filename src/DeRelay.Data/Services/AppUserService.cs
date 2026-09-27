using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

public class AppUserService(DeRelayDbContext deRelayDbContext): IAppUserService
{
    public async Task<int> CreateAppUserAsync(string userName, string passwordHash, int personId)
    {
        var appUser = new AppUser(userName, passwordHash, personId);
        deRelayDbContext.AppUsers.Add(appUser);
        await deRelayDbContext.SaveChangesAsync();
        return appUser.Id;
    }

    public async Task DeleteAppUserByIdAsync(int appUserId)
    {
        deRelayDbContext.AppUsers.Remove(await ReturnAppUserByIdAsync(appUserId));
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<AppUser> ReturnAppUserByIdAsync(int appUserId)
    {
        return await deRelayDbContext.AppUsers.FindAsync(appUserId) ?? 
               throw new NotFoundException($"Account with ID = {appUserId} was not found.");
    }
    
    public async Task<bool> CheckIfUserNameAvailableInAppUser(string userName)
    {
        return await deRelayDbContext.AppUsers.AnyAsync(x => x.UserName == userName);
    }

    /// <remarks>Function designed to return null and throw should be done by the class calls this
    /// function. It's because, this function currently only called by <see cref="AuthService.LoginAsync"/>
    /// and if this <see cref="ReturnAppUserByUsername"/> throw "Username couldn't find", it will
    /// give information that which accounts by name available or not. For that it must be neutral
    /// in throw message that must include both Nickname and Password and since in here only looked
    /// for username, it better to return null and throw done by <see cref="AuthService.LoginAsync"/>
    /// also neutralizing throw message by including Password</remarks>
    public async Task<AppUser?> ReturnAppUserByUsername(string userName)
    {
        return await deRelayDbContext.AppUsers.SingleOrDefaultAsync(x => x.UserName == userName);
    }
}