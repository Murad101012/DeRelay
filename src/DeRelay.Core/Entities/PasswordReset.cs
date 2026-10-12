using System;
using DeRelay.Core.Constants;

namespace DeRelay.Core.Entities;

public class PasswordReset
{
    public string email {get; private set;}
    public string hashedLink {get; private set;}
    public DateTime resetExpire {get; private set;}
    public DateTime linkExpire {get; private set;}

    public PasswordReset(string email, string hashedLink)
    {
        this.email = email;
        this.hashedLink = hashedLink;
        resetExpire = DateTime.UtcNow.AddHours(PasswordResetConstraints.resetExpiryHours);
        linkExpire = DateTime.UtcNow.AddDays(PasswordResetConstraints.linkExpiryDays);
    }
}
