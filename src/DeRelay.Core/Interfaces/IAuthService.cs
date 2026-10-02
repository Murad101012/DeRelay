using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.DTOs.TokenPair;

namespace DeRelay.Core.Interfaces;

//NOTE: Didn't named IAppUserService, because class 
public interface IAuthService
{
    public Task<int> RegisterAsync(RegisterDto dto);
    public Task<JwtAndRefreshTokensDto > LoginAsync(LoginDto dto);
    public Task DeleteAccountAsync(int appUserId);
}