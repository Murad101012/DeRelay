using DeRelay.Core.DTOs.AppUser;

namespace DeRelay.Core.Interfaces;

//NOTE: Didn't named IAppUserService, because class 
public interface IAuthService
{
    public Task<int> RegisterAsync(RegisterDto dto);
    public Task<string> LoginAsync(LoginDto dto);
    public Task DeleteAccountAsync(int appUserId);
}