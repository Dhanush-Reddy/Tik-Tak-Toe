using System.Collections.Concurrent;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Sessions;

/// <summary>
/// The in-memory store of play sessions and the single entry point for game commands.
/// The dictionary only makes lookups thread-safe; atomicity comes from running each whole command
/// (validation, human move, computer reply, score update, response snapshot) under the session lock.
/// </summary>
public sealed class GameSessionService
{
    private readonly ConcurrentDictionary<Guid, PlaySession> _sessions = new();

    public GameStateResponse CreateSession(GameMode mode)
    {
        var session = new PlaySession(mode);
        _sessions[session.Id] = session;
        return WithSession(session.Id, ToResponse);
    }

    public GameStateResponse GetCurrentGame(Guid sessionId) => WithSession(sessionId, ToResponse);

    public GameStateResponse GetGame(Guid sessionId, Guid gameId) =>
        Run(sessionId, session => session.ActiveGame(gameId));

    public GameStateResponse StartNewGame(Guid sessionId, GameMode mode) =>
        Run(sessionId, session => session.StartNewGame(mode));

    public GameStateResponse Play(Guid sessionId, Guid gameId, Player player, int row, int column, int expectedRevision) =>
        Run(sessionId, session => session.Play(gameId, player, row, column, expectedRevision));

    public GameStateResponse Undo(Guid sessionId, Guid gameId, int expectedRevision) =>
        Run(sessionId, session => session.Undo(gameId, expectedRevision));

    public GameStateResponse ResetGame(Guid sessionId, Guid gameId) =>
        Run(sessionId, session => session.ResetGame(gameId));

    public ScoreboardResponse GetScoreboard(Guid sessionId) =>
        WithSession(sessionId, session => ToResponse(session.Scoreboard));

    public ScoreboardResponse ResetScoreboard(Guid sessionId) =>
        WithSession(sessionId, session =>
        {
            session.Scoreboard.Reset();
            return ToResponse(session.Scoreboard);
        });

    /// <summary>Runs a game command under the session lock and returns the resulting state.</summary>
    private GameStateResponse Run(Guid sessionId, Action<PlaySession> command) =>
        WithSession(sessionId, session =>
        {
            command(session);
            return ToResponse(session);
        });

    private T WithSession<T>(Guid sessionId, Func<PlaySession, T> action)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new ResourceNotFoundException(
                ResourceNotFoundException.SessionNotFound,
                "This play session does not exist. Sessions are kept in memory and are lost when the server restarts.");
        }

        lock (session.Sync)
        {
            return action(session);
        }
    }

    private static GameStateResponse ToResponse(PlaySession session)
    {
        var game = session.CurrentGame;
        var outcome = game.Outcome;

        return new GameStateResponse(
            SessionId: session.Id,
            GameId: game.Id,
            Revision: game.Revision,
            Mode: game.Mode,
            Status: outcome.Status,
            CurrentPlayer: game.CurrentPlayer,
            Winner: outcome.Winner,
            WinningCells: outcome.WinningCells,
            Board: game.Board(),
            Moves: [.. game.Moves.Select(m => new MoveResponse(m.Number, m.Player, m.Row, m.Column))],
            CanUndo: game.CanUndo,
            Scoreboard: ToResponse(session.Scoreboard));
    }

    private static ScoreboardResponse ToResponse(Scoreboard scoreboard) =>
        new(scoreboard.XWins, scoreboard.OWins, scoreboard.Draws);
}
