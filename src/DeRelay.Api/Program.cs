using System.Text.Json.Serialization;
using DeRelay.Api.Middlewares;
using DeRelay.Core.Interfaces;
using DeRelay.Core.Validators;
using DeRelay.Data;
using DeRelay.Data.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DeRelayDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IFriendshipService, FriendshipService>();
builder.Services.AddScoped<IFriendRequestService, FriendRequestService>();

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
#endregion


builder.Services.AddControllers()
    //In Enum if 0 = "Male", it will show "Male" in gender, instead of 0.
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var app = builder.Build();

//For swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.MapControllers();

app.Run();
