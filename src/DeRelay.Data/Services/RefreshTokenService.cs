using System.Security.Cryptography;
using System.Text;
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Mappers;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

public class RefreshTokenService(DeRelayDbContext deRelayDbContext): IRefreshTokenService
{
    public async Task<ReturnNewRefreshTokenDto> CreateRefreshTokenWithNewSession(int appUserId)
    {
        var refreshTokenObject = CreateNewRefreshToken(Guid.NewGuid(), appUserId);
        await deRelayDbContext.SaveChangesAsync();
        return refreshTokenObject.ToReturnNewRefreshTokenDto();
    }

    /*TODO: KNOWN PROBLEM: if User holding T refresh token and attacker also holding same token and..
      if they send at the same millisecond both of them can get valid Refresh Token. Since this accident can be
      very unlikely to be happen, for now it's postponed*/
    public async Task<ReturnNewRefreshTokenDto> RefreshTheRefreshTokenOfExistingSession(UserRefreshTokenDto userRefreshTokenDto)
    {
        //Turning User's token to hashed version to compare
        var hashedRefreshToken = TurnUserRefreshTokenToHashedVersion(userRefreshTokenDto.RefreshToken);
        //Finding if hashed token exist
        var oldRefreshToken = await deRelayDbContext.RefreshToken.FirstOrDefaultAsync(
            rf => rf.HashedToken == hashedRefreshToken);
        if (oldRefreshToken == null) throw new NotFoundException("RefreshToken not found, please login again.");
        if (oldRefreshToken.IsRevoked)
        {
            var refreshTokens = await deRelayDbContext.RefreshToken.
                Where(rf => rf.SessionId == oldRefreshToken.SessionId).ToListAsync();
            deRelayDbContext.RefreshToken.RemoveRange(refreshTokens);
            await deRelayDbContext.SaveChangesAsync();
            throw new UnauthorizedException("Session compromised, please login again.");
        }

        ValidateRefreshToken(oldRefreshToken);
        
        //If exist we "disable" the token
        oldRefreshToken.ChangeTokenToRevoked();
        //And create new one
        var refreshToken = CreateNewRefreshToken(oldRefreshToken.SessionId, oldRefreshToken.AppUserId);
        await deRelayDbContext.SaveChangesAsync();
        return refreshToken.ToReturnNewRefreshTokenDto();
    }

    public async Task<RefreshToken> GetRefreshTokenObjectFromUserRefreshTokenString(string refreshToken)
    {
        //Turning User's token to hashed version to compare
        var hashedRefreshToken = TurnUserRefreshTokenToHashedVersion(refreshToken);
        //Finding if hashed token exist and returning
        return await deRelayDbContext.RefreshToken.FirstOrDefaultAsync(
            rf => rf.HashedToken == hashedRefreshToken) ??
               throw new NotFoundException("RefreshToken not found");
    }

    public async Task<List<ReturnSessionDto>> ReturnAllSessionsAsync(int appUserId)
    {
        var allSessions =
            await deRelayDbContext.RefreshToken.Where
                    (t => t.AppUserId == appUserId && !t.IsRevoked)
                .Select(token => new { SessionId = token.SessionId, SessionExpiry = token.SessionExpiry }).ToListAsync();

        var returnSessionDtos = new List<ReturnSessionDto>();
        for (var i = 0; i < allSessions.Count; i++)
        {
            returnSessionDtos.Add(new ReturnSessionDto(
                SessionId: allSessions[i].SessionId, 
                SessionExpires: allSessions[i].SessionExpiry));
        }
        return returnSessionDtos;
    }

    public async Task DeleteSessionAsync(Guid sessionId, int appUserId)
    {
        var refreshToken = 
            await deRelayDbContext.RefreshToken.
                Where(t => t.SessionId == sessionId && t.AppUserId == appUserId).ToListAsync();
        if (refreshToken.Count == 0) throw new NotFoundException("Session not found");
        deRelayDbContext.RefreshToken.RemoveRange(refreshToken);
        await deRelayDbContext.SaveChangesAsync();
    }

    private string GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes);
    }

    private string CreateNewRefreshToken(Guid sessionId, int appUserId)
    {
        var refreshToken = GenerateRefreshToken();
        deRelayDbContext.Add(new RefreshToken(
            sessionId, 
            appUserId, 
            TurnUserRefreshTokenToHashedVersion(refreshToken)));
        return refreshToken;
    }

    private string TurnUserRefreshTokenToHashedVersion(string refreshToken)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));
    }

    private void ValidateRefreshToken(RefreshToken refreshToken)
    {
        if(refreshToken.SessionExpiry <= DateTime.UtcNow)
            throw new UnauthorizedException("Session couldn't find or expired, please login again.");
    }
}