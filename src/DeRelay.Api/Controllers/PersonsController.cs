using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DeRelay.Api.Extensions;
using DeRelay.Core.DTOs.Person;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ValidationException = DeRelay.Core.Exceptions.ValidationException;

namespace DeRelay.Api.Controllers;

[EnableRateLimiting("after-login")]
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PersonsController(
    IPersonService personService, 
    IValidator<UpdatePersonDto> updateValidator): ControllerBase
{
    [HttpPut]
    public async Task<ActionResult<ReturnPersonDto>> Update([FromBody] UpdatePersonDto dto)
    {
        var validate = await updateValidator.ValidateAsync(dto);
        //TODO: In future make all errors will be sent, not only First() error.
        if (!validate.IsValid) 
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await personService.UpdatePersonByIdAsync(User.GetAppUserId(), dto);
        //NOTE: Automatically convert returned DTO into json and add respond body
        return Ok(await personService.GetPersonAsDtoByIdAsync(User.GetAppUserId()));
    }
    
    /// <summary>
    /// Fetches a single person by ID.
    /// </summary>
    /// <remarks>Not required to null check for ReturnPersonDTO.
    /// <see cref="IPersonService.GetPersonAsDtoByIdAsync"/> already null check
    /// and return error code automatically with middleware
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<ReturnPersonDto>> GetById()
    {
        /*NOTE: "Result OK" automatically send alongside under the hood,
          if object successfully returned (without null causing in function)*/
        return await personService.GetPersonAsDtoByIdAsync(User.GetAppUserId());
    }
}