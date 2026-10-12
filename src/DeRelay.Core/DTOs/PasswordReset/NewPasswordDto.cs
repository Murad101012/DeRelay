namespace DeRelay.Core.DTOs.PasswordReset;

public record NewPasswordDto(
    string Token,
    string Password,
    string ConfirmPassword);