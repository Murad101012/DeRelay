using DeRelay.Core.DTOs.Person;
using DeRelay.Core.Entities;

namespace DeRelay.Core.Mappers;

/// <summary>
/// Automate conversion between <see cref="Person"/> and DTOs as
/// <see cref="CreatePersonDto"/>, <see cref="ReturnPersonDto"/>
/// </summary>
public static class PersonMapper
{
    /// <summary>
    /// Converts <see cref="Person"/> Entity to <see cref="ReturnPersonDto"/>
    /// </summary>
    public static ReturnPersonDto ToReturnDto(this Person person)
        => new(
            Id: person.Id,
            FirstName: person.FirstName,
            LastName: person.LastName,
            NickName: person.NickName,
            Gender: person.Gender,
            DateOfBirth: person.DateOfBirth,
            Age: person.Age);

    /// <summary>
    /// Converts <see cref="CreatePersonDto"/> to <see cref="Person"/> Entity
    /// </summary>
    public static Person ToEntity(this CreatePersonDto dto)
        => new(
            firstName: dto.FirstName,
            lastName: dto.LastName,
            nickName: dto.Nickname,
            gender: dto.Gender,
            dateOfBirth: dto.DateOfBirth);

}
