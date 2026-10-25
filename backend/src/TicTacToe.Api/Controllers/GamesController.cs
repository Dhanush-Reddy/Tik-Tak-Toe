using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Sessions;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:guid}/games")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class GamesController(GameSessionService sessions) : ControllerBase
{
    /// <summary>Starts a new game in the given mode (used to switch modes). The scoreboard is kept.</summary>
    [HttpPost]
    [ProducesResponseType<GameStateResponse>(StatusCodes.Status201Created)]
    public ActionResult<GameStateResponse> Start(Guid sessionId, NewGameRequest request)
    {
        var state = sessions.StartNewGame(sessionId, request.Mode);
        return CreatedAtAction(nameof(Get), new { sessionId, gameId = state.GameId }, state);
    }

    [HttpGet("{gameId:guid}")]
    public GameStateResponse Get(Guid sessionId, Guid gameId) => sessions.GetGame(sessionId, gameId);

    /// <summary>Submits a move. In Computer mode the response already includes the computer's reply.</summary>
    [HttpPost("{gameId:guid}/moves")]
    public GameStateResponse Move(Guid sessionId, Guid gameId, MoveRequest request) =>
        sessions.Play(sessionId, gameId, request.Player, request.Row, request.Column, request.ExpectedRevision);

    /// <summary>Undoes the last move (Two Player) or the last move pair (Computer).</summary>
    [HttpPost("{gameId:guid}/undo")]
    public GameStateResponse Undo(Guid sessionId, Guid gameId, UndoRequest request) =>
        sessions.Undo(sessionId, gameId, request.ExpectedRevision);

    /// <summary>Replaces this game with a fresh one in the same mode and returns the new game.</summary>
    [HttpPost("{gameId:guid}/reset")]
    public GameStateResponse Reset(Guid sessionId, Guid gameId) => sessions.ResetGame(sessionId, gameId);
}
