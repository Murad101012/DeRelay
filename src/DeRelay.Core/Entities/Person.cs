using DeRelay.Core.Enums;

namespace DeRelay.Core.Entities;

public class Person
{
    public int Id { get; private set; }
    public string Firstname { get; private set; }
    public string Lastname { get; private set; }
    public string Nickname { get; private set; }
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
    
    public Person(int id, string firstname, string lastname, string nickname, Gender gender, DateTime dateOfBirth)
    {
        #region Exception check before assigning
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id), "Id must be greater than zero");
        if (string.IsNullOrWhiteSpace(firstname)) throw new ArgumentNullException(nameof(firstname), "First name cannot be empty");
        if (string.IsNullOrWhiteSpace(lastname)) throw new ArgumentNullException(nameof(lastname), "Last name cannot be empty");
        if (string.IsNullOrWhiteSpace(nickname)) throw new ArgumentNullException(nameof(nickname), "Nickname cannot be empty");
        if (dateOfBirth > DateTime.UtcNow) throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));
        #endregion
        
        Id = id;
        Firstname = firstname;
        Lastname = lastname;
        Nickname = nickname;
        Gender = gender;
        DateOfBirth = dateOfBirth;
    }
    
    public void SetNickname(string nickname) => Nickname = nickname;
    
    public void SetGender(Gender gender) => Gender = gender;
    
}