using DeRelay.Core.Constants;
using DeRelay.Core.Exceptions;

namespace DeRelay.Core.Entities;

/// <summary>
/// Registration information of <see cref="Person"/>
/// </summary>
public class AppUser
{
    public int Id { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }

    //TODO: Solve accidentaly getting value from PersonId with CI when I learn
    /// <remarks><code>DO NOT GET VALUE FROM THIS VARIABLE!!!</code> Instead use <see cref="ValidatePersonIdAndReturn"/></remarks>
    public int? PersonId { get; private set; }

    public DateTime CreatedOn { get; private set; }
    
    public AppUser(string email, string passwordHash)
    {
        SetEmail(email);
        SetPasswordHash(passwordHash);
        CreatedOn = DateTime.UtcNow;
    }

    public int ValidatePersonIdAndReturn()
    {
        return PersonId ?? throw new NotFoundException("Profile couldn't be found, please create one");
    }
    
    /// <returns>TRUE - PersonId is NOT NULL</returns>
    public bool CheckIfPersonIdExists()
    {
        return PersonId != null;
    }

    public void SetPersonId(int personId)
    {
        if (personId <= 0) throw new ValidationException("PersonId cannot be less or equal to zero");
        PersonId = personId;
    }
    
    private void SetEmail(string email)
    {
        Email = email;
    }

    private void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ValidationException("PasswordHash cannot be empty");
        PasswordHash = passwordHash;
    }

    private AppUser(){}
}