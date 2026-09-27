using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.AppUser;
using FluentValidation;

namespace DeRelay.Core.Validators.AppUser;

public class LoginDtoValidator: AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required.")
            .Must(userName => userName.Length >= AppUserConstraints.UserNameLengthMin && 
                              userName.Length <= AppUserConstraints.UserNameLengthMax).
            WithMessage($"Username must be between {AppUserConstraints.UserNameLengthMin} and " +
                        $"{AppUserConstraints.UserNameLengthMax} characters long.");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .Must(password => password.Length >= AppUserConstraints.PasswordLengthMin && 
                              password.Length <= AppUserConstraints.PasswordLengthMax).
            WithMessage($"Password must be between {AppUserConstraints.PasswordLengthMin} and " +
                        $"{AppUserConstraints.PasswordLengthMax} characters long.");
    }
}