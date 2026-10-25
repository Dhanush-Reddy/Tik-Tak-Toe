using TicTacToe.Api.Domain;
using TicTacToe.Api.Sessions;

namespace TicTacToe.Api.Tests;

internal static class Boards
{
    /// <summary>Parses a board such as "XO.|.X.|..O": X, O, '.' for empty; other characters are ignored.</summary>
    public static Player?[] Parse(string text)
    {
        var cells = text
            .Where(c => c is 'X' or 'O' or '.')
            .Select(c => c switch { 'X' => Player.X, 'O' => (Player?)Player.O, _ => null })
            .ToArray();

        return cells.Length == GameRules.CellCount
            ? cells
            : throw new ArgumentException($"Expected 9 cells but found {cells.Length}.", nameof(text));
    }
}

internal static class Moves
{
    /// <summary>Plays the given cell indexes in order as the player to move (always X in Computer mode).</summary>
    public static void Play(Game game, params int[] cells)
    {
        foreach (var cell in cells)
        {
            var player = game.CurrentPlayer ?? throw new InvalidOperationException("The game is already over.");
            game.Play(player, cell / 3, cell % 3, game.Revision);
        }
    }

    /// <summary>Plays cells through the session, so completed games are scored as in production.</summary>
    public static void Play(PlaySession session, params int[] cells)
    {
        foreach (var cell in cells)
        {
            var game = session.CurrentGame;
            var player = game.CurrentPlayer ?? throw new InvalidOperationException("The game is already over.");
            session.Play(game.Id, player, cell / 3, cell % 3, game.Revision);
        }
    }
}

/// <summary>A value-comparable picture of everything observable about a game.</summary>
internal sealed record GameSnapshot(Guid Id, GameMode Mode, int Revision, Player? CurrentPlayer, GameStatus Status, string Board, string History)
{
    public static GameSnapshot Of(Game game) => new(
        game.Id,
        game.Mode,
        game.Revision,
        game.CurrentPlayer,
        game.Outcome.Status,
        string.Concat(game.Board().Select(cell => cell?.ToString() ?? ".")),
        string.Join(" ", game.Moves.Select(m => $"{m.Number}:{m.Player}@{m.Row},{m.Column}")));
}

/// <summary>Known game lines (cell indexes), all checked to be legal and to end exactly as described.</summary>
internal static class Lines
{
    /// <summary>Two Player: X wins the top row on move 5.</summary>
    public static readonly int[] XWinsTopRow = [0, 3, 1, 4, 2];

    /// <summary>Two Player: O wins the middle row on move 6.</summary>
    public static readonly int[] OWinsMiddleRow = [0, 3, 1, 4, 8, 5];

    /// <summary>Two Player: board fills with no line. Final board XOX/XOO/OXX.</summary>
    public static readonly int[] Draw = [0, 1, 2, 4, 3, 5, 7, 6, 8];

    /// <summary>Two Player: X's ninth move fills the board and completes the top row and the anti-diagonal.</summary>
    public static readonly int[] NinthMoveDoubleWin = [0, 3, 1, 5, 4, 7, 6, 8, 2];

    /// <summary>Computer: X plays 0, 8, 6, 7 (O replies 4, 2, 3) and X wins the bottom row.</summary>
    public static readonly int[] ComputerHumanWins = [0, 8, 6, 7];

    /// <summary>Computer: X plays 1, 2, 3 (O replies 4, 0, 8) and O wins the main diagonal.</summary>
    public static readonly int[] ComputerWins = [1, 2, 3];

    /// <summary>Computer: X plays 0, 1, 6, 5, 7 (O replies 4, 2, 3, 8); X's fifth move fills the board.</summary>
    public static readonly int[] ComputerDraw = [0, 1, 6, 5, 7];
}
