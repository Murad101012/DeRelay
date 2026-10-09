using DeRelay.Core.DTOs.FriendRequest;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

/// <summary>
/// Controls FriendRequests table
/// </summary>
public class FriendRequestService(
    DeRelayDbContext deRelayDbContext, 
    IFriendshipService iFriendshipService,
    IPersonService iPersonService,
    IAppUserService iAppUserService):
    IFriendRequestService
{
    public async Task SendFriendRequestAsync(int appUserId, SendFriendRequestDto dto)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        
        //Check sender/receiver is same
        if(CheckSenderReceiverIdIsSame(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId))
            throw new ValidationException("You cannot send a friend request to same persons");
        
        //Check if the person exists
        await ValidatePersonIdIsValid(appUser.ValidatePersonIdAndReturn());
        await ValidatePersonIdIsValid(dto.ReceiverId);
        
        //Check if already in FriendRequests table
        if (await CheckIfAlreadyInRequest(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId))
            throw new AlreadyExistsException("Friend request for these persons are already in proceed");
        
        var newFriendRequest = new FriendRequest(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId);
        
        deRelayDbContext.Add(newFriendRequest);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task AcceptFriendRequestAsync(int appUserId, AcceptFriendRequestDto dto)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);

        //Check sender/receiver is same
        if(CheckSenderReceiverIdIsSame(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId))
            throw new ValidationException("You cannot accept same persons as a friend request");
        
        //Check if the person exists
        await ValidatePersonIdIsValid(appUser.ValidatePersonIdAndReturn());
        await ValidatePersonIdIsValid(dto.ReceiverId);
        
        //Check if already in FriendRequests table
        if (!await CheckIfAlreadyInRequest(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId))
            throw new NotFoundException("Couldn't find friend request for these persons to accept");
        
        var friendRequest =
            await deRelayDbContext.FriendRequests.FindAsync(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId)
            ?? await deRelayDbContext.FriendRequests.FindAsync(dto.ReceiverId, appUser.ValidatePersonIdAndReturn())
            ?? throw new NotFoundException("Friend request not found");
        await iFriendshipService.AddFriendAsync(appUserId, dto.ReceiverId);
        RemoveFriendRequest(friendRequest);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task DeclineFriendRequestAsync(int appUserId, DeclineFriendRequestDto dto)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);

        //Check sender/receiver is same
        if(CheckSenderReceiverIdIsSame(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId))
            throw new ValidationException("You cannot decline same persons as a friend request");
        
        //Check if the person exists
        await ValidatePersonIdIsValid(appUser.ValidatePersonIdAndReturn());
        await ValidatePersonIdIsValid(dto.ReceiverId);
        
        //Check if already in FriendRequests table
        if (!await CheckIfAlreadyInRequest(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId))
            throw new NotFoundException("Couldn't find friend request for these persons to decline");
        
        var friendRequest =
            await deRelayDbContext.FriendRequests.FindAsync(appUser.ValidatePersonIdAndReturn(), dto.ReceiverId)
            ?? await deRelayDbContext.FriendRequests.FindAsync(dto.ReceiverId, appUser.ValidatePersonIdAndReturn())
            ?? throw new NotFoundException("Friend request not found");
        RemoveFriendRequest(friendRequest);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task<ReturnPersonAllFriendRequestReceivedDto> GetAllFriendRequestOfUserReceivedByIdAsync(int appUserId)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        int personId = appUser.ValidatePersonIdAndReturn();
        return new ReturnPersonAllFriendRequestReceivedDto(await deRelayDbContext.FriendRequests
            .AsNoTracking()
            .Where(x => x.ReceiverId == personId)
            .Select(x => x.SenderId)
            .ToListAsync());
    }

    public async Task<ReturnPersonAllFriendRequestSentDto> GetAllFriendRequestOfUserSentByIdAsync(int appUserId)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        int personId = appUser.ValidatePersonIdAndReturn();
        return new ReturnPersonAllFriendRequestSentDto(await deRelayDbContext.FriendRequests
            .AsNoTracking()
            .Where(x => x.SenderId == personId)
            .Select(x => x.ReceiverId)
            .ToListAsync());
    }

    private void RemoveFriendRequest(FriendRequest friendRequest)
    {
        deRelayDbContext.Remove(friendRequest);
    }

    private bool CheckSenderReceiverIdIsSame(int odt1, int odt2) => odt1 == odt2;

    private async Task ValidatePersonIdIsValid(int personId)
    {
        if(!await iPersonService.PersonExistsAsync(personId))
            throw new NotFoundException($"Person with ID {personId} was not found.");
    }

    private async Task<bool> CheckIfAlreadyInRequest(int odt1, int odt2)
    {
        return await deRelayDbContext.FriendRequests.AnyAsync(x =>
            x.ReceiverId == odt1 && x.SenderId == odt2 ||
            x.SenderId == odt1 && x.ReceiverId == odt2);
    }
}