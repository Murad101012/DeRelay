using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.Friendship;
using FluentValidation;

namespace DeRelay.Core.Validators.Friendship;

public class RemoveFriendDtoValidator: AbstractValidator<RemoveFriendDto>
{
    public RemoveFriendDtoValidator()
    {
        RuleFor(x => x.FriendId)
            .GreaterThanOrEqualTo(PersonConstraints.PersonIdMin)
            .WithMessage($"UserId should be equal or greater than {PersonConstraints.PersonIdMin}");
    }
}