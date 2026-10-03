using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IRefreshTokenService
{
    Task<ReturnNewRefreshTokenDto> CreateRefreshTokenWithNewFamily(int appUserId);
    Task<ReturnNewRefreshTokenDto> RefreshTheRefreshTokenOfExistingFamily(UserRefreshTokenDto userRefreshTokenDto);
    Task<RefreshToken> GetRefreshTokenObjectFromUserRefreshTokenString(string refreshToken);
    Task<List<ReturnSessionDto>> ReturnAllSessionsAsync(int appUserId);
    Task DeleteSessionAsync(Guid familyId, int appUserId);
}