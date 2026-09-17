namespace DeRelay.Core.Constants;

/// <summary>
/// Static variable limit rules for <see cref="Entities.Person"/>
/// </summary>
public static class PersonConstraints
{
    public const int PersonIdMin = 1;
    public const int FirstNameMax = 50;
    public const int FirstNameMin = 2;
    public const int LastNameMax = 50;
    public const int LastNameMin = 2;
    public const int NickNameMax = 20;
    public const int NickNameMin = 2;
}