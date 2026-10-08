using DeRelay.Core.Constants;

namespace DeRelay.Core.Entities;

public class PendingRegistration
{
    public string Hash;
    public string Email { get; private set; }
    public string HashedPassword { get; private set; }
    public DateTime ConfirmationExpiry { get; private set; }
    public DateTime LinkExpiry { get; private set; }

    public PendingRegistration(string email, string hashedPassword)
    {
        Email = email;
        HashedPassword = hashedPassword;
        ConfirmationExpiry = DateTime.UtcNow.AddDays(PendingRegistrationConstraints.ConfirmationExpiryDays);
        LinkExpiry = DateTime.UtcNow.AddDays(PendingRegistrationConstraints.LinkExpiryDays);
    }

    public void SetHash(string hash) => Hash = hash;
}