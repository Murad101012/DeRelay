using DeRelay.Core.DTOs.RefreshToken;

namespace DeRelay.Core.Mappers;

public static class RefreshTokenMapper
{
    public static ReturnNewRefreshTokenDto ToReturnNewRefreshTokenDto(this string refreshToken)
    {
        return new ReturnNewRefreshTokenDto(refreshToken);
    }
}