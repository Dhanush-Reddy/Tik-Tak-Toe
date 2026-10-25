namespace TicTacToe.Api.Domain;

/// <summary>
/// Chooses the computer's (O's) cell using the assessment's priorities, in order:
/// win, block X, center, corner, any empty cell. Within a priority the lowest row-major
/// cell index wins, so the choice is deterministic. This is intentionally simple: it is
/// not minimax and it can be beaten.
/// </summary>
public static class ComputerStrategy
{
    private const int Center = 4;
    private static readonly int[] Corners = [0, 2, 6, 8];

    /// <exception cref="InvalidOperationException">The board is full.</exception>
    public static int ChooseCell(IReadOnlyList<Player?> board)
    {
        bool IsEmpty(int cell) => board[cell] is null;
        var emptyCells = Enumerable.Range(0, GameRules.CellCount).Where(IsEmpty).ToList();

        // Candidates in priority order; First() stops at the first one, so later steps are never evaluated.
        return emptyCells.Where(cell => CompletesLine(board, cell, Player.O)) // 1. win
            .Concat(emptyCells.Where(cell => CompletesLine(board, cell, Player.X))) // 2. block X
            .Concat(new[] { Center }.Where(IsEmpty)) // 3. center
            .Concat(Corners.Where(IsEmpty)) // 4. corner
            .Concat(emptyCells) // 5. any empty cell
            .First();
    }

    /// <summary>
    /// True if <paramref name="player"/> taking the empty <paramref name="cell"/> would complete a line.
    /// It only reads the board; no hypothetical move is ever placed.
    /// </summary>
    private static bool CompletesLine(IReadOnlyList<Player?> board, int cell, Player player) =>
        GameRules.WinningLines.Any(line => line.Contains(cell) && line.All(i => i == cell || board[i] == player));
}
