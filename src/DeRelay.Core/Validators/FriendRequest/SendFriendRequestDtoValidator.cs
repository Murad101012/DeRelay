using DeRelay.Core.Constants;
using DeRelay.Core.DTOs.FriendRequest;
using FluentValidation;

namespace DeRelay.Core.Validators.FriendRequest;

public class SendFriendRequestDtoValidator: AbstractValidator<SendFriendRequestDto>
{
    public SendFriendRequestDtoValidator()
    {
        RuleFor(x => x.ReceiverId)
            .GreaterThanOrEqualTo(PersonConstraints.PersonIdMin)
            .WithMessage($"ReceiverId must be equal or greater than {PersonConstraints.PersonIdMin}");
    }
}