namespace DeRelay.Core.Exceptions;

/// <summary>
/// Base exception class of all custom exception class.
/// </summary>
/// <remarks>Any new custom exceptions (such as <see cref="NotFoundException"/>)
/// should inherit from <c>DomainException</c>.</remarks>
/*NOTE: Custom Exception classes and it's children created and begin to used
  those instead of C# exception, sincerely to make sure all the errors we threw
  comes from our Exception Classes. We need to separate exceptions from C#'s own
  errors (Which other libraries, frameworks also uses), because exceptions out of
  our controls (Meaning an error happen that not comes from out new threw), includes
  very detailed ex.Message that can be leak if we send this back accidentally to
  client (This scenario happen, when EF Core threw error, it cached by:
  "GlobalExceptionMiddleware.cs". When "GlobalExceptionMiddleware.cs" was checking,
  which error this, it was "ArgumentException". Since in the very next line, it
  include ex.Message in Client on ProblemDetails to inform about the error, it
  accidentally send detailed report of EF Core and leaked some arguments, function
  of database (It's like, instead of user get: "Person with "Ivan" couldn't found",
  it get: "'Ivan' couldn't found in PostgresSql server in Name row of Persons Table,
  when this called from PersonService.cs with Person.Name parameter". This class
  and it's children targeted to solve this. In Switch (GlobalExceptionMiddleware),
  we only check Exception that inherits from DomainException (Which our Base Exception)
  and GlobalException simply checks only our exception. Otherwise it will drop to,
  default where it will send a generic ex.Message, since anything drop to this,
  meaning exception that out of our control.
  */
public abstract class DomainException(string message) : Exception(message)
{
  
};