namespace DeRelay.Core.DTOs.RefreshToken;

/// <summary>
/// A new refresh token created in server and for returning to the user to use it for next time
/// </summary>
public record ReturnNewRefreshTokenDto
(
    string RefreshToken
);