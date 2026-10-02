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
    public async Task<ReturnNewRefreshTokenDto> CreateRefreshTokenWithNewFamily(int appUserId)
    {
        var refreshTokenObject = CreateNewRefreshToken(Guid.NewGuid(), appUserId);
        await deRelayDbContext.SaveChangesAsync();
        return refreshTokenObject.ToReturnNewRefreshTokenDto();
    }

    public async Task<ReturnNewRefreshTokenDto> RefreshTokenOfExistingFamily(UserRefreshTokenDto userRefreshTokenDto)
    {
        //Turning User's token to hashed version to compare
        using var sha = SHA256.Create();
        var hashedRefreshToken = 
            Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(userRefreshTokenDto.RefreshToken)));
        //Finding if hashed token exist
        var oldRefreshToken = await deRelayDbContext.RefreshToken.FirstOrDefaultAsync(
            rf => rf.HashedToken == hashedRefreshToken);
        if (oldRefreshToken == null) throw new NotFoundException("RefreshToken not found, please login again.");
        if (oldRefreshToken.IsRevoked)
        {
            var refreshTokens = await deRelayDbContext.RefreshToken.
                Where(rf => rf.FamilyId == oldRefreshToken.FamilyId).ToListAsync();
            deRelayDbContext.RefreshToken.RemoveRange(refreshTokens);
            await deRelayDbContext.SaveChangesAsync();
            throw new AlreadyExistsException("RefreshToken already exists, please login again.");
        }
        //If exist we "disable" the token
        oldRefreshToken.ChangeTokenToRevoked();
        //And create new one
        var refreshToken = CreateNewRefreshToken(oldRefreshToken.FamilyId, oldRefreshToken.AppUserId);
        await deRelayDbContext.SaveChangesAsync();
        return refreshToken.ToReturnNewRefreshTokenDto();
    }
    
    private string GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(randomBytes);
    }

    private string CreateNewRefreshToken(Guid familyId, int appUserId)
    {
        var refreshToken = GenerateRefreshToken();
        using var sha = SHA256.Create();
        var hashedRefreshToken = 
            Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));
        deRelayDbContext.Add(new RefreshToken(familyId, appUserId, hashedRefreshToken));
        return refreshToken;
    }
}