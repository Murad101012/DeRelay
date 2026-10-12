using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IPasswordResetService
{
    Task<string> Create(string email);
    Task Delete(PasswordReset passwordReset);
    Task<bool> CheckLink(string link);
    Task<PasswordReset> ReturnByEmailAsync(string email);
    Task<PasswordReset> ReturnByLinkAsync(string resetLink);
    Task<int> RemoveAllExpiredPasswordResetLinks(CancellationToken stoppingToken);
}
