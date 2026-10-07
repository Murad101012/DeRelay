// Tests written by Muse Spark 1.3 AI
// Scenario: bad data somehow skips the DTO validator and reaches the service.
// These tests prove the domain entities themselves refuse it, so neither a
// broken Person nor a broken AppUser can be built (nothing to save).
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Exceptions;

namespace DeRelay.Tests;

public class AuthDomainTests
{
    [Fact]
    public void AppUser_Valid_ConstructsWithNullPersonId()
    {
        // Fresh account: no profile yet is a valid state, not a missing thing.
        var user = new AppUser("aysel@mail.com", "HASH");
        Assert.Equal("aysel@mail.com", user.Email);
        Assert.Null(user.PersonId);
    }

    [Fact]
    public void AppUser_EmptyHash_Throws()
    {
        Assert.Throws<ValidationException>(() => new AppUser("aysel@mail.com", ""));
    }

    [Fact]
    public void Person_EmptyNickName_Throws()
    {
        // Register person half broken -> Person row can never be built.
        Assert.Throws<ValidationException>(() =>
            new Person("Aysel", "Mammadova", "", Gender.Female, new DateTime(2000, 1, 1)));
    }
}
