using DeRelay.Core.DTOs.Friendship;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

/// <summary>
/// Controls Friendship table
/// </summary>
public class FriendshipService(
    DeRelayDbContext deRelayDbContext, 
    IPersonService iPersonService,
    IAppUserService iAppUserService): IFriendshipService
{
    //Note: Not using DTO as parameter because this wasn't directly called by user anyway only in-server
    public async Task AddFriendAsync(int appUserId, int personId)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        int friendId = appUser.ValidatePersonIdAndReturn();
        
        //Check sender/receiver is same
        if(CheckSenderReceiverIdIsSame(friendId, personId))
            throw new ValidationException("Same person cannot be friend of itself");
        
        //Check if the person exists
        await CheckPersonIdIsValid(friendId);
        await CheckPersonIdIsValid(personId);
        
        int user1IdMin = Math.Min(friendId, personId);
        int user2IdMax = Math.Max(friendId, personId);
        
        //Check if already in Friendship table and assign to variable
        
        if(await CheckIfAlreadyInFriendshipTable(user1IdMin, user2IdMax) != null) 
            throw new AlreadyExistsException("It's already your friend");
        
        deRelayDbContext.Friendships.Add(new Friendship(user1IdMin, user2IdMax));
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task RemoveFriendAsync(int appUserId, RemoveFriendDto dto)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);

        if(CheckSenderReceiverIdIsSame(appUser.ValidatePersonIdAndReturn(), dto.FriendId))
            throw new ValidationException("Same person cannot be friend of itself");
        
        await CheckPersonIdIsValid(appUser.ValidatePersonIdAndReturn());
        await CheckPersonIdIsValid(dto.FriendId);
        
        int person1IdMin = Math.Min(appUser.ValidatePersonIdAndReturn(), dto.FriendId);
        int person2IdMax = Math.Max(appUser.ValidatePersonIdAndReturn(), dto.FriendId);
        
        //Check if already in Friendship table and assign to variable
        var friendShip = await CheckIfAlreadyInFriendshipTable(person1IdMin, person2IdMax) ?? 
                         throw new NotFoundException("Can't remove friend, it is already not your friend");
        
        deRelayDbContext.Friendships.Remove(friendShip);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<ReturnFriendsDto> GetAllFriendsOfUserByIdAsync(int appUserId)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        int personId = appUser.ValidatePersonIdAndReturn();
        await CheckPersonIdIsValid(personId);
        return new ReturnFriendsDto(await deRelayDbContext.Friendships
            .AsNoTracking()
            .Where(x => x.User2Id == personId || x.User1Id == personId)
            .Select(x => x.User1Id == personId ? x.User2Id : x.User1Id)
            .ToListAsync());
    }
    
    private bool CheckSenderReceiverIdIsSame(int person1, int person2) => person1 == person2;
    
    private async Task CheckPersonIdIsValid(int personId)
    {
        if(!await iPersonService.PersonExistsAsync(personId))
            throw new NotFoundException($"Person with ID {personId} was not found.");
    }
    
    private async Task<Friendship?> CheckIfAlreadyInFriendshipTable(int userIdMin, int userIdMax)
    {
        /*Note: Can't sure which one to use between FindAsync / AnyAsync...
          It's because, FindAsync only checks PK which what I need, to check-up faster,
          but can't add .AsNoTracking() overload and this cause to also load the object.
          In the other hand, AnyAsync uses OR, AND filters method so it takes more to query,
          but it let to add .AsNoTracking() so can make read-only... 
          Since Delete method will need object itself to remove, Add FindAsync*/
        return await deRelayDbContext.Friendships.FindAsync(userIdMin, userIdMax);
    }
}