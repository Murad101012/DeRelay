using DeRelay.Core.DTOs.Friendship;

namespace DeRelay.Core.Interfaces;

public interface IFriendshipService
{
    public Task AddFriendAsync(int appUserId, int personId);
    public Task RemoveFriendAsync(int appUserId, RemoveFriendDto dto);
    public Task<ReturnFriendsDto> GetAllFriendsOfUserByIdAsync(int appUserId);
}