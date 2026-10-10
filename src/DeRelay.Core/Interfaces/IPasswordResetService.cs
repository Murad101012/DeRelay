using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IPasswordResetService
{
    Task Create(string email);
    Task Delete(PasswordReset passwordReset);
    Task<PasswordReset> ValidateLink(string link);
    Task<PasswordReset> ReturnByEmailAsync(string email);
}
