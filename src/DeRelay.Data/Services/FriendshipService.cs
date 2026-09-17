using DeRelay.Core.DTOs.Friendship;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

/// <summary>
/// Controls Friendship table
/// </summary>
public class FriendshipService(DeRelayDbContext deRelayDbContext, IPersonService iPersonService): IFriendshipService
{
    //Note: Not using DTO as parameter because this wasn't directly called by user anyway only in-server
    public async Task AddFriendAsync(int user1Id, int user2Id)
    {
        //Check sender/receiver is same
        if(CheckSenderReceiverIdIsSame(user1Id, user2Id))
            throw new ValidationException("Same person cannot be friend of itself");
        
        //Check if the person exists
        await CheckPersonIdIsValid(user1Id);
        await CheckPersonIdIsValid(user2Id);
        
        int user1IdMin = Math.Min(user1Id, user2Id);
        int user2IdMax = Math.Max(user1Id, user2Id);
        
        //Check if already in Friendship table and assign to variable
        
        if(await CheckIfAlreadyInFriendshipTable(user1IdMin, user2IdMax) != null) 
            throw new AlreadyExistsException("It's already your friend");
        
        deRelayDbContext.Friendships.Add(new Friendship(user1IdMin, user2IdMax));
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task RemoveFriendAsync(RemoveFriendDto dto)
    {
        if(CheckSenderReceiverIdIsSame(dto.User1Id, dto.User2Id))
            throw new ValidationException("Same person cannot be friend of itself");
        
        await CheckPersonIdIsValid(dto.User1Id);
        await CheckPersonIdIsValid(dto.User2Id);
        
        int user1IdMin = Math.Min(dto.User1Id, dto.User2Id);
        int user2IdMax = Math.Max(dto.User1Id, dto.User2Id);
        
        //Check if already in Friendship table and assign to variable
        var friendShip = await CheckIfAlreadyInFriendshipTable(user1IdMin, user2IdMax) ?? 
                         throw new NotFoundException("Can't remove friend, it is not your friend");
        
        deRelayDbContext.Friendships.Remove(friendShip);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<ReturnFriendsDto> GetAllFriendsOfUserByIdAsync(int userId)
    {
        await CheckPersonIdIsValid(userId);
        return new ReturnFriendsDto(await deRelayDbContext.Friendships
            .AsNoTracking()
            .Where(x => x.User2Id == userId || x.User1Id == userId)
            .Select(x => x.User1Id == userId ? x.User2Id : x.User1Id)
            .ToListAsync());
    }
    
    private bool CheckSenderReceiverIdIsSame(int odt1, int odt2) => odt1 == odt2;
    
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