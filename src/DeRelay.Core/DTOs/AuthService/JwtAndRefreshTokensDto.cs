namespace DeRelay.Core.DTOs.TokenPair;

public record JwtAndRefreshTokensDto(
    string JwtToken,
    string RefreshToken);