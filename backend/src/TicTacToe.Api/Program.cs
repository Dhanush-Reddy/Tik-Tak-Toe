using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Errors;
using TicTacToe.Api.Sessions;

var builder = WebApplication.CreateBuilder(args);

// All game state lives in this singleton, in memory. Restarting the API clears it.
builder.Services.AddSingleton<GameSessionService>();

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ApiExceptionFilter>();
        // A body that fails to parse already reports why; skip the extra "request field is required" error.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        // Report validation errors under JSON property names ("row"), not C# names ("Row").
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(options =>
    {
        var defaultFactory = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            var result = defaultFactory(context);
            if (result is ObjectResult { Value: ProblemDetails problem })
            {
                problem.Extensions["code"] = "ValidationFailed";
            }

            return result;
        };
    });

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(); // Unexpected errors become a generic problem+json 500 without a stack trace.
app.UseStatusCodePages(); // Empty 404/405/415 responses get a problem+json body too.

app.MapControllers();

app.Run();

static void ConfigureJson(JsonSerializerOptions json)
{
    // Enums travel as exact names ("X", "Computer", "InProgress"); numbers and other spellings are rejected.
    json.Converters.Add(new StrictEnumConverter());
    // ASP.NET's web defaults would also accept numbers written as strings ("row": "1").
    json.NumberHandling = JsonNumberHandling.Strict;
    // The client sends intent only; extra properties such as "board" are a 400, not silently ignored.
    json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
}

/// <summary>Exposed so the integration tests can host the API with WebApplicationFactory.</summary>
public partial class Program;
