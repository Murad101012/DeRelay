using DeRelay.Core.DTOs;
using FluentValidation;
using DeRelay.Core.Constants;

namespace DeRelay.Core.Validators;

public class UpdatePersonDtoValidator: AbstractValidator<UpdatePersonDto>
{
    public UpdatePersonDtoValidator()
    {
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

        
        RuleFor(x => x.Gender).
            IsInEnum().WithMessage("Gender is not valid");
    }
}