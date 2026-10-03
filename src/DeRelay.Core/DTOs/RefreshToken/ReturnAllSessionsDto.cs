namespace DeRelay.Core.DTOs.RefreshToken;

public record ReturnSessionDto(
    Guid SessionId,
    DateTime SessionExpires);