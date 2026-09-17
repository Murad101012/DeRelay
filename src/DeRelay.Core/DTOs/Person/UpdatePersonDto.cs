using DeRelay.Core.Enums;

namespace DeRelay.Core.DTOs.Person;

public record UpdatePersonDto(
    string FirstName,
    string LastName,
    string NickName,
    Gender Gender
    );