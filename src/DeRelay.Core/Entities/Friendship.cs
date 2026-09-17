namespace DeRelay.Core.Entities;

public class Friendship
{
    public int User1Id { get; private set; }
    public int User2Id { get; private set; }
    public DateTime CreatedOn { get; private set; }

    //For when create a new object
    public Friendship(int user1Id, int user2Id)
    {
        User1Id = user1Id;
        User2Id = user2Id;
        CreatedOn = DateTime.UtcNow;
    }

    //NOTE: Required by EF Core to read
    private Friendship(){}
}