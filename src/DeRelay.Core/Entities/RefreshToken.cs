using DeRelay.Core.Constants;

namespace DeRelay.Core.Entities;

public class RefreshToken
{
    public int Id { get; private set; } //This variable only created to satisfy EF Core's primary key requirement
    public Guid FamilyId { get; private set; }  //FamilyId unique for per login
    public DateTime FamilyExpiry { get; private set; }
    public int AppUserId { get; private set; }
    public string HashedToken { get; private set; }
    public bool IsRevoked { get; private set; }

    public RefreshToken(Guid familyId, int appUserId, string hashedToken)
    {
        FamilyId = familyId;
        FamilyExpiry = DateTime.UtcNow + TimeSpan.FromDays(RefreshTokenConstraints.ExpandingDays);
        AppUserId = appUserId;
        HashedToken = hashedToken;
        IsRevoked = false;
    }
    
    public void ChangeTokenToRevoked() => IsRevoked = true;
    
    private RefreshToken(){}
}