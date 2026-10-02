using DeRelay.Core.DTOs.RefreshToken;

namespace DeRelay.Core.Interfaces;

public interface IRefreshTokenService
{
    Task<ReturnNewRefreshTokenDto> CreateRefreshTokenWithNewFamily(int appUserId);
    Task<ReturnNewRefreshTokenDto> RefreshTokenOfExistingFamily(UserRefreshTokenDto userRefreshTokenDto);
}