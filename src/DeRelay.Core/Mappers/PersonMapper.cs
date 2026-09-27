using DeRelay.Core.DTOs.AppUser;
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
            FirstName: person.FirstName,
            LastName: person.LastName,
            NickName: person.NickName,
            Gender: person.Gender,
            DateOfBirth: person.DateOfBirth,
            Age: person.Age);

    /// <summary>
    /// Converts <see cref="CreatePersonDto"/> to <see cref="Person"/> Entity
    /// </summary>
    public static Person ToPersonEntity(this CreatePersonDto dto)
        => new(
            firstName: dto.FirstName,
            lastName: dto.LastName,
            nickName: dto.NickName,
            gender: dto.Gender,
            dateOfBirth: dto.DateOfBirth);
    
    /// <summary>
    /// Converts <see cref="RegisterDto"/> to <see cref="CreatePersonDto"/> Entity
    /// </summary>
    public static CreatePersonDto ToCreatePersonDto(this RegisterDto dto)
        => new(
            FirstName: dto.FirstName,
            LastName: dto.LastName,
            NickName: dto.NickName,
            Gender: dto.Gender,
            DateOfBirth: dto.DateOfBirth);

}
