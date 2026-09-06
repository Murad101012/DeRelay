using DeRelay.Core.DTOs;
using DeRelay.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ValidationException = DeRelay.Core.Exceptions.ValidationException;

namespace DeRelay.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonsController(
    IPersonService personService, 
    IValidator<CreatePersonDto> createValidator,
    IValidator<UpdatePersonDto> updateValidator): ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReturnPersonDto>> 
        Create([FromBody] CreatePersonDto dto)
    {
        //Validate
        var validate = await createValidator.ValidateAsync(dto);
        //TODO: In future make all errors will be sent, not only First() error.
        if (!validate.IsValid) 
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        //Creation
        var createdPersonId = await personService.CreatePersonAsync(dto);
        return CreatedAtAction(nameof(GetById), 
            new {id = createdPersonId}, 
            await personService.GetPersonAsDtoByIdAsync(createdPersonId));
    }
    
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReturnPersonDto>> Update(int id, [FromBody] UpdatePersonDto dto)
    {
        var validate = await updateValidator.ValidateAsync(dto);
        //TODO: In future make all errors will be sent, not only First() error.
        if (!validate.IsValid) 
            throw new ValidationException(validate.Errors.First().ErrorMessage);
        
        await personService.UpdatePersonByIdAsync(id, dto);
        //NOTE: Automatically convert returned DTO into json and add respond body
        return Ok(await personService.GetPersonAsDtoByIdAsync(id));
    }
    
    /// <summary>
    /// Fetches a single person by ID.
    /// </summary>
    /// <remarks>Not required to null check for ReturnPersonDTO.
    /// <see cref="IPersonService.GetPersonAsDtoByIdAsync"/> already null check
    /// and return error code automatically with middleware
    /// </remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReturnPersonDto>> GetById(int id)
    {
        /*NOTE: "Result OK" automatically send alongside under the hood,
          if object successfully returned (without null causing in function)*/
        return await personService.GetPersonAsDtoByIdAsync(id);
    }

    [HttpDelete("{id:int}")]
    /*NOTE: Using IActionResult, instead of ActionResult,
      because we don't return any data on Body*/
    public async Task<IActionResult> Delete(int id)
    {
        await personService.DeletePersonByIdAsync(id);
        return NoContent();
    }
}