// Tests written by Muse Spark 1.3 AI
// Scenario: bad data somehow skips the DTO validator and reaches the service.
// These tests prove the domain entities themselves refuse it, so neither a
// broken Person nor a broken AppUser can be built (nothing to save).
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Mappers;
using FluentValidation.TestHelper;
using DeRelay.Core.Validators.AppUser;

namespace DeRelay.Tests;

public class AuthDomainTests
{
    [Fact]
    public void AppUser_Valid_ConstructsAndKeepsPersonId()
    {
        var user = new AppUser("aysel97", "HASH", 7);
        Assert.Equal("aysel97", user.UserName);
        Assert.Equal(7, user.PersonId);
    }

    [Fact]
    public void AppUser_EmptyUserName_Throws()
    {
        // Login half broken, person half fine -> AppUser row can never be built.
        Assert.Throws<ValidationException>(() => new AppUser("", "HASH", 7));
    }

    [Fact]
    public void AppUser_ShortUserName_Throws()
    {
        Assert.Throws<ValidationException>(() => new AppUser("ab", "HASH", 7));
    }

    [Fact]
    public void AppUser_EmptyHash_Throws()
    {
        Assert.Throws<ValidationException>(() => new AppUser("aysel97", "", 7));
    }

    [Fact]
    public void Person_EmptyNickName_Throws()
    {
        // Register person half broken -> Person row can never be built.
        Assert.Throws<ValidationException>(() =>
            new Person("Aysel", "Mammadova", "", Gender.Female, new DateTime(2000, 1, 1)));
    }

    [Fact]
    public void Register_BadNickName_ValidatorFails_And_DomainThrows()
    {
        // Same bad value through both layers: validator says 400,
        // and if skipped, the entity constructor still throws.
        var bad = new RegisterDto("aysel97", "cat12345", "Aysel", "Mammadova", "", Gender.Female, new DateTime(2000, 1, 1));

        new RegisterDtoValidator().TestValidate(bad)
            .ShouldHaveValidationErrorFor(x => x.NickName);

        Assert.Throws<ValidationException>(() => bad.ToCreatePersonDto().ToPersonEntity());
    }
}
