using System;
using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.PasswordReset;
using FluentValidation;

namespace DeRelay.Core.Validators.PasswordReset;

public class NewPasswordDtoValidator : AbstractValidator<NewPasswordDto>
{
    public NewPasswordDtoValidator()
    {
        RuleFor(x => x).
            Must(x => x.Password == x.ConfirmPassword).
            WithMessage("Passwords aren't matching");

        RuleFor(x => x.Password).
            NotEmpty().WithMessage("Password cannot be empty");
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .Must(password => password.Length >= AppUserConstraints.PasswordLengthMin && 
                              password.Length <= AppUserConstraints.PasswordLengthMax).
            WithMessage($"Password must be between {AppUserConstraints.PasswordLengthMin} and " +
                        $"{AppUserConstraints.PasswordLengthMax} characters long.");

        RuleFor(x => x.ConfirmPassword).
            NotEmpty().WithMessage("Confirm password cannot be empty and must match to Password");
    }
}
