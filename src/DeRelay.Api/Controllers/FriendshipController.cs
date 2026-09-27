
using DeRelay.Api.Extensions;
using DeRelay.Core.DTOs.Friendship;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeRelay.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FriendshipController(
    IFriendshipService iFriendshipService
    ,IValidator<RemoveFriendDto> removeValidator): ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] RemoveFriendDto dto)
    {
        var validate = await removeValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await iFriendshipService.RemoveFriendAsync(User.GetAppUserId(), dto);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<ReturnFriendsDto>> Get()
    {
        return await iFriendshipService.GetAllFriendsOfUserByIdAsync(User.GetAppUserId());
    }
}