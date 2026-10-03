using DeRelay.Core.Constants;

namespace DeRelay.Core.Entities;

public class RefreshToken
{
    public int Id { get; private set; } //This variable only created to satisfy EF Core's primary key requirement
    public Guid SessionId { get; private set; }  //Unique for per login
    public DateTime SessionExpiry { get; private set; }
    public int AppUserId { get; private set; }
    public string HashedToken { get; private set; }
    public bool IsRevoked { get; private set; }

    public RefreshToken(Guid sessionId, int appUserId, string hashedToken)
    {
        SessionId = sessionId;
        SessionExpiry = DateTime.UtcNow + TimeSpan.FromDays(RefreshTokenConstraints.ExpandingDays);
        AppUserId = appUserId;
        HashedToken = hashedToken;
        IsRevoked = false;
    }
    
    public void ChangeTokenToRevoked() => IsRevoked = true;
    
    private RefreshToken(){}
}