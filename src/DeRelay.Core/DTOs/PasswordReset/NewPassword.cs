using System;

namespace DeRelay.Core.DTOs.PasswordReset;

public record NewPassword(
    string newPassword,
    string newPasswordVerify
);
