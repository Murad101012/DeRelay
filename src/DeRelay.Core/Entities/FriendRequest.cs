namespace DeRelay.Core.Entities;

/// <summary>
/// Table for requests are sent but hasn't responded yet
/// </summary>
public class FriendRequest
{
    public int SenderId { get; private set; }
    public int ReceiverId { get; private set; }
    public DateTime CreatedOn { get; private set; }

    //For when create a new object
    public FriendRequest(int senderId, int receiverId)
    {
        SenderId = senderId;
        ReceiverId = receiverId;
        CreatedOn = DateTime.UtcNow;
    }

    //NOTE: Required by EF Core to read
    private FriendRequest(){}
}