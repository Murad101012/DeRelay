using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IPendingRegistrationService
{
    Task Create(string email, string hashedPassword);
    Task Delete(PendingRegistration pendingRegistration);
    Task<PendingRegistration?> ReturnPendingRegistrationByEmail(string email);
    /// <summary>
    /// Validates confirmation link and if it's passes, returns <see cref="PendingRegistration"/>
    /// </summary>
    /// <param name="confirmationLink">Variable value in the link after '?key='/</param>
    /// <returns></returns>
    Task<PendingRegistration> ValidateConfirmationLink(string confirmationLink);
}