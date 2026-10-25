using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Sessions;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/sessions/{sessionId:guid}/scoreboard")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class ScoreboardController(GameSessionService sessions) : ControllerBase
{
    [HttpGet]
    public ScoreboardResponse Get(Guid sessionId) => sessions.GetScoreboard(sessionId);

    /// <summary>Clears the counters only; the current game is left as it is.</summary>
    [HttpPost("reset")]
    public ScoreboardResponse Reset(Guid sessionId) => sessions.ResetScoreboard(sessionId);
}
