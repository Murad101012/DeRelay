using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.AppUser;
using FluentValidation;

namespace DeRelay.Core.Validators.AppUser;

public class RegisterDtoValidator: AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        //AppUser
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
        
        
        //Person
        RuleFor(x => x.FirstName).
            NotEmpty().WithMessage("First name cannot be empty").
            MaximumLength(PersonConstraints.FirstNameMax).WithMessage($"First name cannot be longer than {PersonConstraints.FirstNameMax}").
            MinimumLength(PersonConstraints.FirstNameMin).WithMessage($"First name must be at least {PersonConstraints.FirstNameMin}");
        
        RuleFor(x => x.LastName).
            NotEmpty().WithMessage("Last name cannot be empty").
            MaximumLength(PersonConstraints.LastNameMax).WithMessage($"Last name cannot be longer than {PersonConstraints.LastNameMax}").
            MinimumLength(PersonConstraints.LastNameMin).WithMessage($"Last name must be at least {PersonConstraints.LastNameMin}");
        
        RuleFor(x => x.NickName).
            NotEmpty().WithMessage("Nickname cannot be empty").
            MinimumLength(PersonConstraints.NickNameMin).WithMessage($"Nickname must be at least {PersonConstraints.NickNameMin}").
            MaximumLength(PersonConstraints.NickNameMax).WithMessage($"Nickname cannot be longer than {PersonConstraints.NickNameMax}");
        
        RuleFor(x => x.DateOfBirth).
            NotEmpty().WithMessage("Date of birth cannot be empty").
            LessThan(DateTime.UtcNow).WithMessage("Date of birth cannot be in the future");
        
        RuleFor(x => x.Gender).
            IsInEnum().WithMessage("Gender is not valid");
    }
}