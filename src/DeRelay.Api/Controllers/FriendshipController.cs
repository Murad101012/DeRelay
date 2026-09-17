
using DeRelay.Core.DTOs.Friendship;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace DeRelay.Api.Controllers;

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
        
        await iFriendshipService.RemoveFriendAsync(dto);
        return NoContent();
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<ReturnFriendsDto>> Get(int userId)
    {
        return await iFriendshipService.GetAllFriendsOfUserByIdAsync(userId);
    }
}