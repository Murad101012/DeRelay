using DeRelay.Core.DTOs.Friendship;

namespace DeRelay.Core.Interfaces;

public interface IFriendshipService
{
    public Task AddFriendAsync(int user1Id, int user2Id);
    public Task RemoveFriendAsync(RemoveFriendDto dto);
    public Task<ReturnFriendsDto> GetAllFriendsOfUserByIdAsync(int userId);
}