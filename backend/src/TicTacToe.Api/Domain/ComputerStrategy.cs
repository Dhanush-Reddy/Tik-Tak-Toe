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

    public static int ChooseCell(IReadOnlyList<Player?> board)
    {
        var emptyCells = Enumerable.Range(0, GameRules.CellCount).Where(cell => board[cell] is null).ToList();
        if (emptyCells.Count == 0)
        {
            throw new InvalidOperationException("The computer cannot move on a full board.");
        }

        bool IsEmpty(int cell) => board[cell] is null;

        return FirstWhere(emptyCells, cell => CompletesLine(board, cell, Player.O)) // 1. win
            ?? FirstWhere(emptyCells, cell => CompletesLine(board, cell, Player.X)) // 2. block X
            ?? FirstWhere([Center], IsEmpty) // 3. center
            ?? FirstWhere(Corners, IsEmpty) // 4. corner
            ?? emptyCells[0]; // 5. any empty cell
    }

    /// <summary>
    /// True if <paramref name="player"/> taking the empty <paramref name="cell"/> would complete a line.
    /// It only reads the board; no hypothetical move is ever placed.
    /// </summary>
    private static bool CompletesLine(IReadOnlyList<Player?> board, int cell, Player player) =>
        GameRules.WinningLines.Any(line => line.Contains(cell) && line.All(i => i == cell || board[i] == player));

    /// <summary>The first matching cell in ascending order, or null.</summary>
    private static int? FirstWhere(IEnumerable<int> cells, Func<int, bool> predicate)
    {
        foreach (var cell in cells)
        {
            if (predicate(cell))
            {
                return cell;
            }
        }

        return null;
    }
}
