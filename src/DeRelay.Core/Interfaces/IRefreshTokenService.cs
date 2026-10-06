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
    /// <summary>
    /// Deletes old refresh tokens in session if that session contains over <see cref="DeRelay.Core.Constants.RefreshTokenConstraints.RefreshTokenHoldingLimitForSession"/>
    /// </summary>
    /// <returns>How many rows deleted</returns>
    Task<int> DeleteOldRefreshTokensInSessionsAsync(CancellationToken cancellationToken);
}