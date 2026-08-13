using DeRelay.Core.Entities;
using DeRelay.Core.Enums;

Person person = new Person(id: 3039432, firstname: "Imelda", lastname: "Ponce", dateOfBirth: new DateTime(2003, 7, 12), gender: Gender.Female, nickname: "LilCups");

Console.Write($"Your birthday is: {person.DateOfBirth.ToString("dd/MM/yyyy")}, so that makes you {person.Age} years old.");