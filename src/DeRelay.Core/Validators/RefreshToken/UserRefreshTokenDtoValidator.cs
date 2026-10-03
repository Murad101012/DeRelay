using DeRelay.Core.DTOs.RefreshToken;
using FluentValidation;

namespace DeRelay.Core.Validators.RefreshToken;

public class UserRefreshTokenDtoValidator: AbstractValidator<UserRefreshTokenDto>
{
    public UserRefreshTokenDtoValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Refresh token is required");
    }
}