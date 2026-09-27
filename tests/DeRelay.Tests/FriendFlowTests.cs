// Tests written by Muse Spark 1.3 AI
using DeRelay.Core.DTOs.FriendRequest;
using DeRelay.Core.DTOs.Friendship;
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Core.Validators.FriendRequest;
using DeRelay.Core.Validators.Friendship;
using FluentValidation.TestHelper;

namespace DeRelay.Tests;

public class FriendFlowTests
{
    // --- Person creation: correct + mistakes ---

    [Fact]
    public void CreatePerson_Valid_DoesNotThrow()
    {
        var person = new Person("Jo", "Doe", "jodoe", Gender.Male, new DateTime(2000, 1, 1));
        Assert.Equal("Jo", person.FirstName);
    }

    [Fact]
    public void CreatePerson_EmptyFirstName_Throws()
    {
        Assert.Throws<Core.Exceptions.ValidationException>(() =>
            new Person("", "Doe", "jodoe", Gender.Male, new DateTime(2000, 1, 1)));
    }

    [Fact]
    public void CreatePerson_FutureBirth_Throws()
    {
        Assert.Throws<Core.Exceptions.ValidationException>(() =>
            new Person("Jo", "Doe", "jodoe", Gender.Male, DateTime.UtcNow.AddDays(1)));
    }

    // --- Send validator (slim DTO: ReceiverId only; self-check lives in service) ---

    [Fact]
    public void SendValidator_Correct_Passes()
    {
        var v = new SendFriendRequestDtoValidator();
        var result = v.TestValidate(new SendFriendRequestDto(2));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SendValidator_ZeroId_Fails()
    {
        var v = new SendFriendRequestDtoValidator();
        var result = v.TestValidate(new SendFriendRequestDto(0));
        result.ShouldHaveValidationErrorFor(x => x.ReceiverId);
    }

    [Fact]
    public void SendValidator_NegativeId_Fails()
    {
        var v = new SendFriendRequestDtoValidator();
        var result = v.TestValidate(new SendFriendRequestDto(-5));
        result.ShouldHaveValidationErrorFor(x => x.ReceiverId);
    }

    // --- Accept: correct + mistake ---

    [Fact]
    public void AcceptValidator_Correct_Passes()
    {
        var v = new AcceptFriendRequestDtoValidator();
        var result = v.TestValidate(new AcceptFriendRequestDto(2));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AcceptValidator_ZeroId_Fails()
    {
        var v = new AcceptFriendRequestDtoValidator();
        var result = v.TestValidate(new AcceptFriendRequestDto(0));
        result.ShouldHaveValidationErrorFor(x => x.ReceiverId);
    }

    // --- Decline: correct + mistake ---

    [Fact]
    public void DeclineValidator_Correct_Passes()
    {
        var v = new DeclineFriendRequestDtoValidator();
        var result = v.TestValidate(new DeclineFriendRequestDto(2));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeclineValidator_ZeroId_Fails()
    {
        var v = new DeclineFriendRequestDtoValidator();
        var result = v.TestValidate(new DeclineFriendRequestDto(0));
        result.ShouldHaveValidationErrorFor(x => x.ReceiverId);
    }

    // --- Remove friend: correct + mistake ---

    [Fact]
    public void RemoveFriendValidator_Correct_Passes()
    {
        var v = new RemoveFriendDtoValidator();
        var result = v.TestValidate(new RemoveFriendDto(2));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RemoveFriendValidator_ZeroId_Fails()
    {
        var v = new RemoveFriendDtoValidator();
        var result = v.TestValidate(new RemoveFriendDto(0));
        result.ShouldHaveValidationErrorFor(x => x.FriendId);
    }
}
