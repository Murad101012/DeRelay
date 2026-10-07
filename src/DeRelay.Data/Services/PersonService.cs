using DeRelay.Core.DTOs.Person;
using DeRelay.Core.Entities;
using DeRelay.Core.Exceptions;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Mappers;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

public class PersonService(
    DeRelayDbContext deRelayDbContext,
    IAppUserService iAppUserService): IPersonService
{
    public async Task<int> CreatePersonAsync(CreatePersonDto dto)
    {
        var newPerson = dto.ToPersonEntity();
        deRelayDbContext.Persons.Add(newPerson);
        await deRelayDbContext.SaveChangesAsync();
        return newPerson.Id;
    }

    public async Task<ReturnPersonDto> GetPersonAsDtoByIdAsync(int appUserId)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        var person = await GetPersonReadOnlyByIdAsync(appUser.ValidatePersonIdAndReturn());
        return person.ToReturnDto();
    }

    public async Task UpdatePersonByIdAsync(int appUserId, UpdatePersonDto dto)
    {
        var appUser = await iAppUserService.ReturnAppUserByIdAsync(appUserId);
        var person = await GetPersonByIdAsync(appUser.ValidatePersonIdAndReturn());
        person.UpdatePerson(dto.FirstName, dto.LastName, dto.NickName, dto.Gender);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task DeletePersonByIdAsync(int personId)
    {
        deRelayDbContext.Persons.Remove(await GetPersonByIdAsync(personId));
        await deRelayDbContext.SaveChangesAsync();
    }
    
    private async Task<Person> GetPersonByIdAsync(int personId)
    {
        return await deRelayDbContext.Persons.FindAsync(personId) ?? 
               throw new NotFoundException($"Person with ID = {personId} was not found.");
    }

    private async Task<Person> GetPersonReadOnlyByIdAsync(int personId)
    {
        return await deRelayDbContext.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(person => person.Id == personId) ??
               throw new NotFoundException($"Person with ID = {personId} was not found.");
    }

    /// <summary>
    /// Checks if person exist in database without loading the actual person
    /// </summary>
    public async Task<bool> PersonExistsAsync(int personId)
    {
        return await deRelayDbContext.Persons.AnyAsync(person => person.Id == personId);
    }
}