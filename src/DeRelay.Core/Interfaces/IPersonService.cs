using DeRelay.Core.DTOs.Person;

namespace DeRelay.Core.Interfaces;

public interface IPersonService
{
    Task<int> CreatePersonAsync(CreatePersonDto dto);
    Task<ReturnPersonDto> GetPersonAsDtoByIdAsync(int appUserId);
    Task UpdatePersonByIdAsync(int appUserId, UpdatePersonDto dto);
    Task DeletePersonByIdAsync(int personId);
    Task<bool> PersonExistsAsync(int personId);
}