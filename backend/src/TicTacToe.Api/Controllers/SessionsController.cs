using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Sessions;

namespace TicTacToe.Api.Controllers;

/// <summary>A play session owns one scoreboard and one active game.</summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(GameSessionService sessions) : ControllerBase
{
    /// <summary>Creates a session with an empty scoreboard and its first game.</summary>
    [HttpPost]
    public ActionResult<GameStateResponse> Create(NewGameRequest request)
    {
        var state = sessions.CreateSession(request.Mode);
        return CreatedAtAction(nameof(Get), new { sessionId = state.SessionId }, state);
    }

    /// <summary>Returns the session's active game, used to resume after a page refresh.</summary>
    [HttpGet("{sessionId:guid}")]
    public GameStateResponse Get(Guid sessionId) => sessions.GetCurrentGame(sessionId);
}
