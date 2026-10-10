namespace DeRelay.Core.Constants;

public static class AppUserConstraints
{
    //NOTE: Min rules will be inside of Database with CHECK, because EF Core not support it
    public const int EmailLengthMax = 254;
    
    public const int PasswordLengthMin = 8;
    public const int PasswordLengthMax = 100;
}