using DeRelay.Core.DTOs;
using DeRelay.Core.Entities;
using DeRelay.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data.Services;

public class PersonService(DeRelayDbContext deRelayDbContext): IPersonService
{
    public async Task<int> CreatePersonAsync(CreatePersonDto dto)
    {
        var newPerson = new Person(
            firstName: dto.FirstName,
            lastName: dto.LastName,
            nickName: dto.Nickname,
            gender: dto.Gender,
            dateOfBirth: dto.DateOfBirth);
        
        deRelayDbContext.Persons.Add(newPerson);
        
        await deRelayDbContext.SaveChangesAsync();
        
        return newPerson.Id;
    }

    public async Task<ReturnPersonDto> GetPersonAsDtoByIdAsync(int id)
    {
        var person = await GetPersonReadOnlyByIdAsync(id);
        var returnPersonDto = new ReturnPersonDto(
            Id: person.Id, 
            FirstName: person.FirstName,
            LastName: person.LastName,
            NickName: person.NickName,
            Gender: person.Gender,
            DateOfBirth: person.DateOfBirth,
            Age: person.Age
            );
        return returnPersonDto;
    }

    public async Task UpdatePersonByIdAsync(int id, UpdatePersonDto dto)
    {
        var person = await GetPersonByIdAsync(id);
        person.UpdatePerson(dto.FirstName, dto.LastName, dto.NickName, dto.Gender);
        await deRelayDbContext.SaveChangesAsync();
    }

    public async Task DeletePersonByIdAsync(int id)
    {
        deRelayDbContext.Persons.Remove(await GetPersonByIdAsync(id));
        await deRelayDbContext.SaveChangesAsync();
    }
    
    private async Task<Person> GetPersonByIdAsync(int id)
    {
        return await deRelayDbContext.Persons.FindAsync(id) ?? 
               throw new KeyNotFoundException($"Person with ID {id} was not found.");
    }

    private async Task<Person> GetPersonReadOnlyByIdAsync(int id)
    {
        return await deRelayDbContext.Persons
            .AsNoTracking()
            .FirstOrDefaultAsync(person => person.Id == id) ??
               throw new KeyNotFoundException($"Person with ID {id} was not found.");
    }

}