using DeRelay.Core.DTOs.FriendRequest;
using DeRelay.Core.DTOs.Friendship;

namespace DeRelay.Core.Interfaces;

public interface IFriendRequestService
{
    public Task SendFriendRequestAsync(int appUserId, SendFriendRequestDto dto);
    public Task AcceptFriendRequestAsync(int appUserId, AcceptFriendRequestDto dto);
    public Task DeclineFriendRequestAsync(int appUserId, DeclineFriendRequestDto dto);
    public Task<ReturnPersonAllFriendRequestReceivedDto> GetAllFriendRequestOfUserReceivedByIdAsync(int appUserId);
    public Task<ReturnPersonAllFriendRequestSentDto> GetAllFriendRequestOfUserSentByIdAsync(int appUserId);
}