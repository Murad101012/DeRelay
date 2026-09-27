// Tests written by Muse Spark 1.3 AI
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.Enums;
using DeRelay.Core.Validators.AppUser;
using FluentValidation.TestHelper;

namespace DeRelay.Tests;

public class AuthValidatorTests
{
    private static RegisterDto ValidRegister(string userName = "aysel97", string password = "cat12345") =>
        new(userName, password, "Aysel", "Mammadova", "aysel", Gender.Female, new DateTime(2000, 1, 1));

    [Fact]
    public void Register_Correct_Passes()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister())
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Register_ShortUserName_Fails()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister("ab"))
            .ShouldHaveValidationErrorFor(x => x.UserName);
    }

    [Fact]
    public void Register_LongUserName_Fails()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister(new string('a', 17)))
            .ShouldHaveValidationErrorFor(x => x.UserName);
    }

    [Fact]
    public void Register_BoundaryLengths_Pass()
    {
        // Min 4 and Max 16 are both valid ("between" is inclusive).
        new RegisterDtoValidator().TestValidate(ValidRegister(new string('a', 4)))
            .ShouldNotHaveValidationErrorFor(x => x.UserName);
        new RegisterDtoValidator().TestValidate(ValidRegister(new string('a', 16)))
            .ShouldNotHaveValidationErrorFor(x => x.UserName);
    }

    [Fact]
    public void Register_ShortPassword_Fails()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister(password: "short"))
            .ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Login_Correct_Passes()
    {
        new LoginDtoValidator().TestValidate(new LoginDto("aysel97", "cat12345"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Login_ShortUserName_Fails()
    {
        new LoginDtoValidator().TestValidate(new LoginDto("ab", "cat12345"))
            .ShouldHaveValidationErrorFor(x => x.UserName);
    }
}
