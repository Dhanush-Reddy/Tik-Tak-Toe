using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Contracts;

/// <summary>
/// Everything the UI needs to render a game, taken from one consistent snapshot.
/// <see cref="Board"/> and <see cref="WinningCells"/> use row-major cell indexes (row * 3 + column);
/// rows and columns are zero-based.
/// </summary>
public sealed record GameStateResponse(
    Guid SessionId,
    Guid GameId,
    int Revision,
    GameMode Mode,
    GameStatus Status,
    Player? CurrentPlayer,
    Player? Winner,
    IReadOnlyList<int> WinningCells,
    IReadOnlyList<Player?> Board,
    IReadOnlyList<MoveResponse> Moves,
    bool CanUndo,
    ScoreboardResponse Scoreboard);

public sealed record MoveResponse(int Number, Player Player, int Row, int Column);

public sealed record ScoreboardResponse(int XWins, int OWins, int Draws);
