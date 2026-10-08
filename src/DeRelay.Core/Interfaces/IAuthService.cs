using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.DTOs.Person;
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.DTOs.TokenPair;
using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Creating AppUser
    /// </summary>
    /// <remarks>In this step, only Account (AppUser) created.
    /// Actual Profile (Person) created with <see cref="CompleteProfile"/></remarks>
    public Task RegisterAsPending(RegisterDto dto);
    public Task AcceptConfirmationLink(string link);
    public Task<int> CompleteProfile(CreatePersonDto dto, int appUserId);
    public Task<JwtAndRefreshTokensDto> LoginAsync(LoginDto dto);
    public Task DeleteAccountAsync(int appUserId);

    public Task<JwtAndRefreshTokensDto> RefreshJwtAndRefreshTokensAsync
        (UserRefreshTokenDto dto);

}