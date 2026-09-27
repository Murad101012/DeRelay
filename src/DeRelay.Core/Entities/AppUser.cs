using DeRelay.Core.Constants;
using DeRelay.Core.Exceptions;

namespace DeRelay.Core.Entities;

/// <summary>
/// Registration information of <see cref="Person"/>
/// </summary>
public class AppUser
{
    public int Id { get; private set; }
    public string UserName { get; private set; }
    public string PasswordHash { get; private set; }
    public int PersonId { get; private set; }
    public DateTime CreatedOn { get; private set; }
    
    public AppUser(string userName, string passwordHash, int personId)
    {
        SetUserName(userName);
        SetPasswordHash(passwordHash);
        PersonId = personId;
        CreatedOn = DateTime.UtcNow;
    }
    
    private void SetUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new ValidationException("UserName cannot be empty");
        if (userName.Length < AppUserConstraints.UserNameLengthMin || userName.Length > AppUserConstraints.UserNameLengthMax)
            throw new ValidationException($"UserName should be between {AppUserConstraints.UserNameLengthMin}" +
                                          $" and {AppUserConstraints.UserNameLengthMax} characters");
        UserName = userName;
    }

    private void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ValidationException("PasswordHash cannot be empty");
        PasswordHash = passwordHash;
    }

    private AppUser(){}
}