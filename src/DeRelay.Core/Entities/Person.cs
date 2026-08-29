using DeRelay.Core.Enums;

namespace DeRelay.Core.Entities;

public class Person
{
    public int Id { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string NickName { get; private set; }
    public Gender Gender { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    
    public int Age
    {
        get
        {
            //Calculate roughly by year
            var today = DateTime.UtcNow;
            var age = today.Year - DateOfBirth.Year;
            
            //Then checks by month/days that if current date already passed it birthday.
            if (today.Month < DateOfBirth.Month || (today.Month == DateOfBirth.Month && today.Day < DateOfBirth.Day))
            {
                //If didn't pass, we decrease the age by one before returning
                age--;
            }
            return age;
        }
    }
    
    public Person(string firstName, string lastName, string nickName, Gender gender, DateTime dateOfBirth)
    {
        SetFirstName(firstName);
        SetLastName(lastName);
        SetNickname(nickName);
        SetDateOfBirth(dateOfBirth);
        SetGender(gender);
    }

    public void UpdatePerson(string firstName, string lastName, string nickName, Gender gender)
    {
        SetFirstName(firstName);
        SetLastName(lastName);
        SetNickname(nickName);
        SetGender(gender);
    }

    private void SetFirstName(string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentNullException(nameof(firstName), "First name cannot be empty");
        FirstName = firstName;
    }

    private void SetLastName(string lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentNullException(nameof(lastName), "Last name cannot be empty");
        LastName = lastName;
    }

    private void SetNickname(string nickName)
    {
        if (string.IsNullOrWhiteSpace(nickName)) throw new ArgumentNullException(nameof(nickName), "Nickname cannot be empty");
        NickName = nickName;
    } 
    
    private void SetDateOfBirth(DateTime dateOfBirth)
    {
        if (dateOfBirth > DateTime.UtcNow) throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));
        DateOfBirth = dateOfBirth;
    }

    private void SetGender(Gender gender)
    {
        Gender = gender;
    }
}