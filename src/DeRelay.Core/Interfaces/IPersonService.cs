using DeRelay.Core.DTOs;
using DeRelay.Core.DTOs.Person;

namespace DeRelay.Core.Interfaces;

public interface IPersonService
{
    Task<int> CreatePersonAsync(CreatePersonDto dto);
    Task<ReturnPersonDto> GetPersonAsDtoByIdAsync(int id);
    Task UpdatePersonByIdAsync(int id, UpdatePersonDto dto);
    Task DeletePersonByIdAsync(int id);
    Task<bool> PersonExistsAsync(int id);
}