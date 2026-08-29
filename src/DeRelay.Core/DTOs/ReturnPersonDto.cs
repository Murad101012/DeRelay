using DeRelay.Core.Enums;

namespace DeRelay.Core.DTOs;

public record ReturnPersonDto(
    int Id,
    string FirstName,
    string LastName,
    string NickName,
    Gender Gender,
    DateTime DateOfBirth,
    int Age
);
    