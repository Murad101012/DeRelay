using DeRelay.Core.Enums;

namespace DeRelay.Core.DTOs.AppUser;

public record RegisterDto(
    //AppUser
    string UserName,
    string Password,

    //Person
    string FirstName,
    string LastName,
    string NickName,
    Gender Gender,
    DateTime DateOfBirth
);

    
