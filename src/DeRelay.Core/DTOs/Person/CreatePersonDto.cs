using DeRelay.Core.Enums;

namespace DeRelay.Core.DTOs.Person;

public record CreatePersonDto(
    string FirstName,
    string LastName,
    string NickName,
    Gender Gender,
    DateTime DateOfBirth
    );