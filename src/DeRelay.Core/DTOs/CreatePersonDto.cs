using DeRelay.Core.Enums;

namespace DeRelay.Core.DTOs;

public record CreatePersonDto(
    string FirstName,
    string LastName,
    string Nickname,
    Gender Gender,
    DateTime DateOfBirth
    );