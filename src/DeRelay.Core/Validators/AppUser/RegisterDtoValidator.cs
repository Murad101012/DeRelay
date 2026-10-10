using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.AppUser;
using FluentValidation;

namespace DeRelay.Core.Validators.AppUser;

public class RegisterDtoValidator: AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(AppUserConstraints.EmailLengthMax).WithMessage("Email is too long.")
            .EmailAddress().WithMessage("Email format is invalid.");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .Must(password => password.Length >= AppUserConstraints.PasswordLengthMin && 
                              password.Length <= AppUserConstraints.PasswordLengthMax).
            WithMessage($"Password must be between {AppUserConstraints.PasswordLengthMin} and " +
                        $"{AppUserConstraints.PasswordLengthMax} characters long.");

        RuleFor(x => x).Must(x => x.Password == x.PasswordVerify).
            WithMessage("Passwords don't match, please be sure passwords are same");
    }
}