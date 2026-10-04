using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DeRelay.Api.Extensions;
using DeRelay.Api.Middlewares;
using DeRelay.Core.Entities;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Validators.Person;
using DeRelay.Core.Validators.FriendRequest;
using DeRelay.Core.Validators.Friendship;
using DeRelay.Core.Validators.AppUser;
using DeRelay.Data;
using DeRelay.Data.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DeRelayDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

#region CustomMade AddScoped
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IFriendshipService, FriendshipService>();
builder.Services.AddScoped<IFriendRequestService, FriendRequestService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAppUserService, AppUserService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
#endregion

#region Framework AddScoped
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
#endregion

var secretKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing");

builder.Services.AddSingleton<SigningCredentials>(
    new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        SecurityAlgorithms.HmacSha256));

//For swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#region FluentValidation
#region Person
builder.Services.AddValidatorsFromAssemblyContaining<CreatePersonDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<UpdatePersonDtoValidator>();
#endregion
#region FriendRequest
builder.Services.AddValidatorsFromAssemblyContaining<SendFriendRequestDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<AcceptFriendRequestDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<DeclineFriendRequestDtoValidator>();
#endregion
#region Friendship
builder.Services.AddValidatorsFromAssemblyContaining<RemoveFriendDtoValidator>();
#endregion
#region AppUser
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();
#endregion
#endregion

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Slow down and try again in a bit."
        }, cancellationToken: cancellationToken);
    };
    
    options.AddPolicy("login-tight", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 7
            }));
    
    options.AddPolicy("register", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 20
            }));
    
    options.AddPolicy("after-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.GetAppUserId(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(30),
                PermitLimit = 30
            }));
    
    options.AddPolicy("refresh", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 4
            }));
});

builder.Services.AddControllers()
    //In Enum if 0 = "Male", it will show "Male" in gender, instead of 0.
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

                                    //NOTE: Just returns "Bearer" string
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        //These two validate signature with secret key
        ValidateIssuerSigningKey = true, 
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        //Check if token hasn't expired yet (exp tag)
        ValidateLifetime = true,
        //How much server can tolerance if token expired (Value set to none)
        ClockSkew = TimeSpan.Zero,
        //Validate the sender
        /*NOTE: Let's think a scenario where a game have two versions: 1.Product; 2.Staging.
         Product version where we serving to the players and Staging is for developers to test the game.
         Logically, we also need to create two different secret key, database for these game versions.
         ValidateIssuer get handy exactly in this scenario. Because in staging version we can create a
         God-like character with everything unlocked, infinite money. And we create token of this character
         account as "Issuer = game-staging" for stage server/game and also we add "Issuer = game-product" 
         for Product version. Since Full-Unlocked, infinite resource player created with "game-staging"
         this cannot be mixed in the product version because in product version we only accept token that
         created with issuer equal to game-product... meaning product version only accepts tokens created
         by itself. With that, if attacker get our God-Like character's owner account token, it can't use
         in product version, since product version only accept game-product as Issuer.
         TL;DR - Issuer is the name of who created this token*/
        ValidateIssuer = false,
        //Validate what this token for
        /*NOTE: Let's say we have a server that hosting both game and forum of that game. When a user register
         it's account usable for both game and forum (E.g like how with Gmail we can open YouTube and other
         Google products). But let's say in game it's mostly for in-game statics, while in forum it can DM
         with others. Audience tag coming handy in here, because we can separate exactly where the user want to
         log in (Game or Forum). User tell the Audience in the Log In details and server send add this this
         on JWT's audience tag. For attacker side, this can help to reduce the damage. For example, let's say 
         attacker somehow stole user's token. And user asked this token for Game. If attacked change token's
         audience tag from "game" to "forum" it fail BECAUSE OF HMAC, NOT THE ValidateAudience.. then what is
         ValidateAudience preventing? It's preventing if attacked try to use game token to forum token it will be
         rejected.. because if it try to change forum, the signature will be mismatch... if it just send to
         Forum site while JWT token's audience is game.. it will be rejected. Even if the attacker can still do
         some actions on game until the token's exp time out, it at least can't reach to forum and user's DM*/
        ValidateAudience = false,
    });
    
builder.Services.AddAuthorization();

var app = builder.Build();

//For swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();