// Tests written by Muse Spark 1.3 AI
using DeRelay.Core.DTOs.Person;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Mappers;
using DeRelay.Core.Validators;
using FluentValidation.TestHelper;

namespace DeRelay.Tests;

public class PersonTests
{
    // --- Domain: correct + mistakes ---

    [Fact]
    public void CreatePerson_NickName21Chars_PassesDomain()
    {
        // Domain only guards empty; length is validator + DB job.
        // 21 chars passes domain (documenting current split).
        var person = new Person("Jo", "Doe", new string('a', 21), Gender.Male, new DateTime(2000, 1, 1));
        Assert.Equal(21, person.NickName.Length);
    }

    [Fact]
    public void CreatePerson_InvalidGender_Throws()
    {
        Assert.Throws<Core.Exceptions.ValidationException>(() =>
            new Person("Jo", "Doe", "jodoe", (Gender)999, new DateTime(2000, 1, 1)));
    }

    [Fact]
    public void Age_OnBirthday_IsCorrect()
    {
        var today = DateTime.UtcNow;
        var birth = new DateTime(today.Year - 20, today.Month, today.Day);
        var person = new Person("Jo", "Doe", "jodoe", Gender.Female, birth);
        Assert.Equal(20, person.Age);
    }

    // --- Validators: length + enum ---

    [Fact]
    public void CreateValidator_NickName21Chars_Fails()
    {
        var v = new CreatePersonDtoValidator();
        var dto = new CreatePersonDto("Jo", "Doe", new string('a', 21), Gender.Male, new DateTime(2000, 1, 1));
        var result = v.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Nickname);
    }

    [Fact]
    public void CreateValidator_Gender999_Fails()
    {
        var v = new CreatePersonDtoValidator();
        var dto = new CreatePersonDto("Jo", "Doe", "jodoe", (Gender)999, new DateTime(2000, 1, 1));
        var result = v.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.Gender);
    }

    // --- Mapper ---

    [Fact]
    public void ToReturnDto_MapsAllFields()
    {
        var person = new Person("Jo", "Doe", "jodoe", Gender.Male, new DateTime(2000, 1, 1));
        var dto = person.ToReturnDto();
        Assert.Equal(person.FirstName, dto.FirstName);
        Assert.Equal(person.LastName, dto.LastName);
        Assert.Equal(person.NickName, dto.NickName);
        Assert.Equal(person.Gender, dto.Gender);
        Assert.Equal(person.DateOfBirth, dto.DateOfBirth);
        Assert.Equal(person.Age, dto.Age);
    }

    [Fact]
    public void ToEntity_MapsAllFields()
    {
        var dto = new CreatePersonDto("Jo", "Doe", "jodoe", Gender.Female, new DateTime(2000, 1, 1));
        var person = dto.ToEntity();
        Assert.Equal(dto.FirstName, person.FirstName);
        Assert.Equal(dto.Nickname, person.NickName);
        Assert.Equal(dto.Gender, person.Gender);
    }
}
