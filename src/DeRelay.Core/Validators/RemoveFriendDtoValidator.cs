using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.Friendship;
using FluentValidation;

namespace DeRelay.Core.Validators;

public class RemoveFriendDtoValidator: AbstractValidator<RemoveFriendDto>
{
    public RemoveFriendDtoValidator()
    {
        RuleFor(x => x.User1Id)
            .GreaterThanOrEqualTo(PersonConstraints.PersonIdMin)
            .WithMessage($"UserId should be equal or greater than {PersonConstraints.PersonIdMin}");
        RuleFor(x => x.User2Id)
            .GreaterThanOrEqualTo(PersonConstraints.PersonIdMin)
            .WithMessage($"UserId should be equal or greater than {PersonConstraints.PersonIdMin}");
        RuleFor(x => x.User1Id)
            .NotEqual(x => x.User2Id)
            .WithMessage("You cannot remove yourself as friend");
    }
}