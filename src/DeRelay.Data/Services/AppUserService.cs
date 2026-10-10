using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

public class AppUserService(DeRelayDbContext deRelayDbContext): IAppUserService
{
    public async Task<int> CreateAppUserAsync(string email, string passwordHash)
    {
        var appUser = new AppUser(email, passwordHash);
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
    
    public async Task<bool> CheckIfEmailAvailableInAppUser(string email)
    {
        return await deRelayDbContext.AppUsers.AnyAsync(x => x.Email == email);
    }

    /// <remarks>Function designed to return null and throw should be done by the class calls this
    /// function. It's because, this function currently only called by <see cref="AuthService.LoginAsync"/>
    /// and if this <see cref="ReturnAppUserByEmail"/> throw "Email couldn't find", it will
    /// give information that which accounts by name available or not. For that it must be neutral
    /// in throw message that must include both Nickname and Password and since in here only looked
    /// for email, it better to return null and throw done by <see cref="AuthService.LoginAsync"/>
    /// also neutralizing throw message by including Password</remarks>
    public async Task<AppUser?> ReturnAppUserByEmail(string email)
    {
        return await deRelayDbContext.AppUsers.SingleOrDefaultAsync(x => x.Email == email);
    }

    public string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    public async Task<bool> CheckHasProfile(int appUserId)
    {
        var appUser = await deRelayDbContext.AppUsers.FirstOrDefaultAsync(appUser => appUser.Id == appUserId);
        if (appUser == null) throw new NotFoundException("Account couldn't found");
        return appUser.PersonId != null;
    }
}