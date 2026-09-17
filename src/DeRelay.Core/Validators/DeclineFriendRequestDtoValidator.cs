using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.FriendRequest;
using FluentValidation;

namespace DeRelay.Core.Validators;

public class DeclineFriendRequestDtoValidator: AbstractValidator<DeclineFriendRequestDto>
{
    public DeclineFriendRequestDtoValidator()
    {
        RuleFor(x => x.SenderId)
            .GreaterThanOrEqualTo(PersonConstraints.PersonIdMin)
            .WithMessage($"SenderId must be equal or greater than {PersonConstraints.PersonIdMin}");
        RuleFor(x => x.ReceiverId)
            .GreaterThanOrEqualTo(PersonConstraints.PersonIdMin)
            .WithMessage($"ReceiverId must be equal or greater than {PersonConstraints.PersonIdMin}");
        RuleFor(x => x.ReceiverId)
            .NotEqual(x => x.SenderId)
            .WithMessage("You cannot decline a friend request from yourself.");
    }
}