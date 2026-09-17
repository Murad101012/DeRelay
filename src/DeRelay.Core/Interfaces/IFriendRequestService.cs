using DeRelay.Core.DTOs.FriendRequest;
using DeRelay.Core.DTOs.Friendship;

namespace DeRelay.Core.Interfaces;

public interface IFriendRequestService
{
    public Task SendFriendRequestAsync(SendFriendRequestDto dto);
    public Task AcceptFriendRequestAsync(AcceptFriendRequestDto dto);
    public Task DeclineFriendRequestAsync(DeclineFriendRequestDto dto);
    public Task<ReturnPersonAllFriendRequestReceivedDto> GetAllFriendRequestOfUserReceivedByIdAsync(int userId);
    public Task<ReturnPersonAllFriendRequestSentDto> GetAllFriendRequestOfUserSentByIdAsync(int userId);
}