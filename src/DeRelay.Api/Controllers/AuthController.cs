using DeRelay.Api.Extensions;
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ValidationException = DeRelay.Core.Exceptions.ValidationException;

namespace DeRelay.Api.Controllers;

/// <summary>
/// Lifecycle of an Account and jwt handling (by <see cref="Data.Services.AuthService"/>)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IAuthService iAuthService,
    IValidator<RegisterDto> registerValidator,
    IValidator<LoginDto> loginValidator): ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<int>> Register([FromBody] RegisterDto dto)
    {
        var validate = await registerValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);

        return StatusCode(201, await iAuthService.RegisterAsync(dto));
    }

    [HttpPost("login")]
    public async Task<ActionResult<string>> Login([FromBody] LoginDto dto)
    {
        var validate = await loginValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        return StatusCode(200, await iAuthService.LoginAsync(dto));
    }

    [Authorize]
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await iAuthService.DeleteAccountAsync(User.GetAppUserId());
        return NoContent();
    }
}