using DeRelay.Core.Constants;
using DeRelay.Core.DTOs;
using FluentValidation;

namespace DeRelay.Core.Validators;

public class CreatePersonDtoValidator: AbstractValidator<CreatePersonDto>
{
    public CreatePersonDtoValidator()
    {
        RuleFor(x => x.FirstName).
            NotEmpty().WithMessage("First name cannot be empty").
            MaximumLength(PersonConstraints.FirstNameMax).WithMessage($"First name cannot be longer than {PersonConstraints.FirstNameMax}").
            MinimumLength(PersonConstraints.FirstNameMin).WithMessage($"First name must be at least {PersonConstraints.FirstNameMin}");
        
        RuleFor(x => x.LastName).
            NotEmpty().WithMessage("Last name cannot be empty").
            MaximumLength(PersonConstraints.LastNameMax).WithMessage($"Last name cannot be longer than {PersonConstraints.LastNameMax}").
            MinimumLength(PersonConstraints.LastNameMin).WithMessage($"Last name must be at least {PersonConstraints.LastNameMin}");
        
        RuleFor(x => x.Nickname).
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