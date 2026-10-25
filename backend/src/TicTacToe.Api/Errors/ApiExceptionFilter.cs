using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using TicTacToe.Api.Domain;
using TicTacToe.Api.Sessions;

namespace TicTacToe.Api.Errors;

/// <summary>
/// Turns expected rejections into problem+json responses with a stable <c>code</c>.
/// Anything else is left to the global exception handler, which returns a generic 500.
/// </summary>
public sealed class ApiExceptionFilter(ProblemDetailsFactory problemDetailsFactory) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        (int Status, string Title, string Code)? mapping = context.Exception switch
        {
            ResourceNotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Code),
            GameRuleException { Code: GameErrorCode.CellOutOfRange } e => (StatusCodes.Status400BadRequest, "Invalid move", e.Code.ToString()),
            GameRuleException e => (StatusCodes.Status409Conflict, "The request conflicts with the current game state", e.Code.ToString()),
            _ => null,
        };

        if (mapping is not { } error)
        {
            return;
        }

        var problem = problemDetailsFactory.CreateProblemDetails(
            context.HttpContext, error.Status, error.Title, detail: context.Exception.Message);
        problem.Extensions["code"] = error.Code;

        context.Result = new ObjectResult(problem)
        {
            StatusCode = error.Status,
            ContentTypes = { "application/problem+json" },
        };
        context.ExceptionHandled = true;
    }
}
