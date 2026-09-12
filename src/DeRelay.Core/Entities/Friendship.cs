namespace DeRelay.Core.Entities;

public class Friendship
{
    public int UserId { get; private set; }
    public int FriendId { get; private set; }
    public DateTime CreatedOn { get; private set; }
}