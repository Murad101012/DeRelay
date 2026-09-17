namespace DeRelay.Core.DTOs.Friendship;

public record RemoveFriendDto(
    int User1Id,
    int User2Id);