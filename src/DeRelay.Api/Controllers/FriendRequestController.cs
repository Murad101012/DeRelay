using DeRelay.Core.DTOs.FriendRequest;
using DeRelay.Core.DTOs.Friendship;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace DeRelay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FriendRequestController(
    IFriendRequestService iFriendRequestService
    ,IValidator<SendFriendRequestDto> sendValidator
    ,IValidator<AcceptFriendRequestDto> acceptValidator
    ,IValidator<DeclineFriendRequestDto> deleteValidator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestDto dto)
    {
        //Validate
        var validate = await sendValidator.ValidateAsync(dto);
        if (!validate.IsValid) 
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await iFriendRequestService.SendFriendRequestAsync(dto);
        return Created();
    }
    
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptFriendRequest([FromBody] AcceptFriendRequestDto dto)
    {
        //Validate
        var validate = await acceptValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await iFriendRequestService.AcceptFriendRequestAsync(dto);
        return Created();
    }

    [HttpDelete]
    public async Task<IActionResult> DeclineFriendRequest([FromBody] DeclineFriendRequestDto dto)
    {
        //Validate
        var validate = await deleteValidator.ValidateAsync(dto);
        if (!validate.IsValid)
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await iFriendRequestService.DeclineFriendRequestAsync(dto);
        return NoContent();
    }
    
    [HttpGet("sent/{userId:int:min(1)}")]
    public async Task<ActionResult<ReturnPersonAllFriendRequestSentDto>> GetListOfSendFriendRequest(int userId)
    {
        return await iFriendRequestService.GetAllFriendRequestOfUserSentByIdAsync(userId);
    }
    
    [HttpGet("received/{userId:int:min(1)}")]
    public async Task<ActionResult<ReturnPersonAllFriendRequestReceivedDto>> GetListOfReceivedFriendRequest(int userId)
    {
        return await iFriendRequestService.GetAllFriendRequestOfUserReceivedByIdAsync(userId);
    }
}