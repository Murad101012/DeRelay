namespace DeRelay.Core.DTOs.FriendRequest;

public record AcceptFriendRequestDto (
    int SenderId,
    int ReceiverId
);