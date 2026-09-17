namespace DeRelay.Core.DTOs.FriendRequest;

public record DeclineFriendRequestDto(
    int SenderId,
    int ReceiverId);