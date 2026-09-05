using DeRelay.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace DeRelay.Api.Middlewares;

/// <summary>
/// Catch errors in other logic (With <see cref="Core.DomainException"/>)
/// </summary>
/// <remarks>When it catches error, it checks which exception it throws from the children of
/// <see cref="Core.DomainException"/> and package them into a standard error body and include
/// to HttpContext. If it catches an error that not custom created by developer
/// using <see cref="Core.DomainException"/> it falls into default and send as Server Error</remarks> 
public class GlobalExceptionMiddleware
    (RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    /*NOTE: It must be exactly named InvokeAsync and should accept HttpContext
            parameter*/
    public async Task InvokeAsync(HttpContext httpContext)
    {
        logger.LogInformation("Global exception middleware triggered for request");
        try
        {
            await next(httpContext);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Global exception middleware catch error");
            /*NOTE: Any write operation to Http's body make it automatically send
             and after it send we can't change any part of it. So, we should
             check if HttpContext already begin to send by other class.
             If it sent, then there is no reason to execute "catch" block, since
             it will give error when we try to modify HttpContext*/
            if (httpContext.Response.HasStarted)
            {
                logger.LogWarning("The response has already started, the exception middleware will not be executed.");
                return;
            }
            
            /*Setting the type of data in body, for client browser
             that, client's browser can parse it by knowing the type*/
            httpContext.Response.ContentType = "application/json";
            
            /*NOTE: Instead of creating a struct/class/record and convert to
              JSON manually, we use already prepared class called ProblemDetails*/
            var problemDetails = new ProblemDetails
            {
                Detail = ex.Message
            };

            /*We check, the type of exception the controller throw
              for this case, it's PersonsController.cs*/
            switch (ex)
            {
                case NotFoundException:
                    /*In Web standard, it's common practice to include
                     status code in both in Body and Inline Status code*/
                    problemDetails.Status = StatusCodes.Status404NotFound;
                    problemDetails.Title = "Key not found";
                    break;
                case ValidationException: 
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Title = "Invalid Request Parameter";
                    break;
                case AlreadyExistsException:
                    problemDetails.Status = StatusCodes.Status409Conflict;
                    problemDetails.Title = "Already Exists";
                    break;
                default:
                    problemDetails.Status = StatusCodes.Status500InternalServerError;
                    problemDetails.Title = "Internal Server Error";
                    /*We override Detail with custom ex.message to prevent sending actual error, that
                     can leak program/database structure*/
                    problemDetails.Detail = "An unexpected error occurred. Please try again later.";
                    break;
            }

            //Here we include the status code again, which it's located top of Headers
            httpContext.Response.StatusCode = problemDetails.Status.Value;

            await httpContext.Response.WriteAsJsonAsync(problemDetails);
        }
        logger.LogInformation("Global exception middleware triggered for response");
    }
}