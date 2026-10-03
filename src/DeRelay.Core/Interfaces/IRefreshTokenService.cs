using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.Entities;

namespace DeRelay.Core.Interfaces;

public interface IRefreshTokenService
{
    Task<ReturnNewRefreshTokenDto> CreateRefreshTokenWithNewSession(int appUserId);
    Task<ReturnNewRefreshTokenDto> RefreshTheRefreshTokenOfExistingSession(UserRefreshTokenDto userRefreshTokenDto);
    Task<RefreshToken> GetRefreshTokenObjectFromUserRefreshTokenString(string refreshToken);
    Task<List<ReturnSessionDto>> ReturnAllSessionsAsync(int appUserId);
    Task DeleteSessionAsync(Guid sessionId, int appUserId);
}