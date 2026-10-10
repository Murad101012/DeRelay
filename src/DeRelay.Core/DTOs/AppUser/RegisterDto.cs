using DeRelay.Core.Enums;

namespace DeRelay.Core.DTOs.AppUser;

public record RegisterDto(
    //AppUser
    string Email,
    string Password,
    string PasswordVerify
);

    
