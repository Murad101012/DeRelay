using DeRelay.Api.Extensions;
using DeRelay.Core.DTOs.AppUser;
using DeRelay.Core.DTOs.PasswordReset;
using DeRelay.Core.DTOs.Person;
using DeRelay.Core.DTOs.RefreshToken;
using DeRelay.Core.DTOs.TokenPair;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using ValidationException = DeRelay.Core.Exceptions.ValidationException;

namespace DeRelay.Api.Controllers;

/// <summary>
/// Lifecycle of an Account and jwt handling (by <see cref="Data.Services.AuthService"/>)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IAuthService iAuthService,
    ILogger<AuthController> iLogger,
    IValidator<RegisterDto> registerValidator,
    IValidator<CreatePersonDto> createPersonValidator,
    IValidator<LoginDto> loginValidator,
    IValidator<UserRefreshTokenDto> userRefreshTokenValidator,
    IValidator<NewPasswordDto> newPasswordValidator): ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("create-full-account")]
    public async Task<IActionResult> RegisterAsPending([FromBody] RegisterDto dto)
    {
        var validate = await registerValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);

        await iAuthService.RegisterAsPending(dto);
        
        return StatusCode(201);
    }

    [HttpGet("confirm")]
    [EnableRateLimiting("create-full-account")]
    public async Task<IActionResult> Confirm([FromQuery] string key)
    {
        try
        {
            await iAuthService.AcceptConfirmationLink(key);
            return Content("Link confirmed successfully, you can log in.", "text/plain");
        }
        catch (NotFoundException ex)
        {
            Response.StatusCode = 404;
            return Content(ex.Message, "text/plain");
        }
        catch (UnauthorizedException ex)
        {
            Response.StatusCode = 401;
            return Content(ex.Message, "text/plain");
        }
        catch (AlreadyExistsException ex)
        {
            Response.StatusCode = 409;
            return Content(ex.Message, "text/plain");
        }
    }
    
    [Authorize]
    [HttpPost("complete-profile")]
    [EnableRateLimiting("create-full-account")]
    public async Task<ActionResult<int>> CompleteProfile([FromBody] CreatePersonDto dto)
    {
        var validate = await createPersonValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);

        return StatusCode(201, await iAuthService.CompleteProfile(dto, User.GetAppUserId()));
    }
    
    [HttpPost("login")]
    [EnableRateLimiting("login-tight")]
    public async Task<ActionResult<JwtAndRefreshTokensDto>> Login([FromBody] LoginDto dto)
    {
        var validate = await loginValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        return StatusCode(200, await iAuthService.LoginAsync(dto));
    }

    [HttpPost("send-password-reset-link")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> SendPasswordResetLink([FromBody] PasswordResetEmailDto passwordResetEmail)
    {
        /*NOTE: SendPasswordResetLink() wrapped in try/catch block, because the throws error messages
         can leak knowladge in Database as (Giving false e-mail -> Attacker know there don't 
         an user with this email). Instead of silently returning where the problem happens, 
         it's wrapped in try/catch. With that user get a netural message, while the actual problem
         logged in the server. */
        try
        {
            await iAuthService.SendPasswordResetLink(passwordResetEmail);
        }
        catch (Exception e)
        {
            iLogger.LogError(e, e.Message);
        }
        return Ok("If an account exists for this email, a reset link is on its way. " +
                  "Check your inbox and spam box.");
    }

    [HttpPost("apply-new-password")]
    [EnableRateLimiting("password-reset")]
    public async Task<IActionResult> ChangePassword([FromForm] NewPasswordDto newPassword)
    {
        var validate = await newPasswordValidator.ValidateAsync(newPassword);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        await iAuthService.ApplyNewPassword(newPassword);
        return Ok();
    }

    [Authorize]
    [EnableRateLimiting("after-login")]
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        await iAuthService.DeleteAccountAsync(User.GetAppUserId());
        return NoContent();
    }
    
    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<ActionResult<JwtAndRefreshTokensDto>> Refresh([FromBody] UserRefreshTokenDto dto)
    {
        var validate = await userRefreshTokenValidator.ValidateAsync(dto);
        return !validate.IsValid ? 
            throw new ValidationException(validate.Errors.First().ErrorMessage) : 
            StatusCode(200, await iAuthService.RefreshJwtAndRefreshTokensAsync(dto));
    }
}