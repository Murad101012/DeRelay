// Tests written by Muse Spark 1.3 AI
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.Validators.AppUser;
using FluentValidation.TestHelper;

namespace DeRelay.Tests;

public class AuthValidatorTests
{
    private static RegisterDto ValidRegister(string email = "aysel@mail.com", string password = "cat12345") =>
        new(email, password);

    [Fact]
    public void Register_Correct_Passes()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister())
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Register_NoAtSign_Fails()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister("ayselmail.com"))
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Register_NoDomain_Fails()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister("aysel@"))
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Register_TooLong_Fails()
    {
        new RegisterDtoValidator().TestValidate(ValidRegister(new string('a', 250) + "@b.co"))
            .ShouldHaveValidationErrorFor(x => x.Email);
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
        new LoginDtoValidator().TestValidate(new LoginDto("aysel@mail.com", "cat12345"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Login_BadFormat_Fails()
    {
        new LoginDtoValidator().TestValidate(new LoginDto("ayselmail.com", "cat12345"))
            .ShouldHaveValidationErrorFor(x => x.Email);
    }
}
