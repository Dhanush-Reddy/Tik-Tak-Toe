namespace TicTacToe.Api.Domain;

/// <summary>Pure board rules. A board is 9 cells in row-major order (index = row * 3 + column).</summary>
public static class GameRules
{
    public const int Size = 3;
    public const int CellCount = Size * Size;

    public static IReadOnlyList<IReadOnlyList<int>> WinningLines { get; } =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8], // rows
        [0, 3, 6], [1, 4, 7], [2, 5, 8], // columns
        [0, 4, 8], [2, 4, 6],            // diagonals
    ];

    public static bool IsOnBoard(int row, int column) => row is >= 0 and < Size && column is >= 0 and < Size;

    public static int CellIndex(int row, int column) => row * Size + column;

    /// <summary>
    /// Wins are checked before the draw, so a ninth move that completes a line is a win.
    /// If one move completes several lines, the winning cells are their sorted union.
    /// </summary>
    public static GameOutcome Evaluate(IReadOnlyList<Player?> board)
    {
        Player? winner = null;
        var winningCells = new SortedSet<int>();

        foreach (var line in WinningLines)
        {
            var owner = board[line[0]];
            if (owner is null || board[line[1]] != owner || board[line[2]] != owner)
            {
                continue;
            }

            // Only the player who just moved can complete lines, so in a real game every
            // completed line has the same owner.
            winner ??= owner;
            if (owner == winner)
            {
                winningCells.UnionWith(line);
            }
        }

        if (winner is not null)
        {
            return new GameOutcome(GameStatus.Won, winner, [.. winningCells]);
        }

        return board.All(cell => cell is not null) ? GameOutcome.Draw : GameOutcome.InProgress;
    }
}
