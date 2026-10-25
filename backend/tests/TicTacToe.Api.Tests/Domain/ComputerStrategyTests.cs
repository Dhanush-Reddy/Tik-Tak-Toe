using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Tests.Domain;

/// <summary>
/// Each board is a legal position with O to move (X has exactly one more mark than O, nobody has won).
/// Cell indexes: 0 1 2 / 3 4 5 / 6 7 8.
/// </summary>
public class ComputerStrategyTests
{
    [Fact]
    public void WinsInsteadOfBlocking()
    {
        // O can win at 5; X threatens 2. Winning comes first even though 2 is the lower index.
        Assert.Equal(5, ComputerStrategy.ChooseCell(Boards.Parse("XX.|OO.|X..")));
    }

    [Fact]
    public void BlocksInsteadOfTakingCenter()
    {
        // X threatens the top row at 2; the center is free but blocking comes first.
        Assert.Equal(2, ComputerStrategy.ChooseCell(Boards.Parse("XX.|...|O..")));
    }

    [Fact]
    public void TakesCenterWhenNothingToWinOrBlock()
    {
        Assert.Equal(4, ComputerStrategy.ChooseCell(Boards.Parse("X..|...|...")));
    }

    [Fact]
    public void TakesLowestFreeCornerWhenCenterIsTaken()
    {
        // Center taken, corners 0 and 8 taken: 2 is the lowest free corner (6 is also free).
        Assert.Equal(2, ComputerStrategy.ChooseCell(Boards.Parse("X..|.O.|..X")));
    }

    [Fact]
    public void TakesFirstCornerWhenAllCornersAreFree()
    {
        Assert.Equal(0, ComputerStrategy.ChooseCell(Boards.Parse("...|.X.|...")));
    }

    [Fact]
    public void TakesLowestEmptyCellWhenCenterAndCornersAreTaken()
    {
        // Only edges 3 and 5 are free and neither wins or blocks.
        Assert.Equal(3, ComputerStrategy.ChooseCell(Boards.Parse("XOX|.X.|OXO")));
    }

    [Fact]
    public void ChoosesLowestWinningCellWhenSeveralWin()
    {
        // O wins at 6 (anti-diagonal) or 8 (main diagonal).
        Assert.Equal(6, ComputerStrategy.ChooseCell(Boards.Parse("OXO|XOX|.X.")));
    }

    [Fact]
    public void ChoosesLowestBlockingCellWhenXHasSeveralThreats()
    {
        // X threatens 1 (top row) and 3 (left column); O has no winning move.
        Assert.Equal(1, ComputerStrategy.ChooseCell(Boards.Parse("X.X|.O.|X.O")));
    }

    [Fact]
    public void ChooseCell_DoesNotModifyBoard()
    {
        // Win and block checks consider hypothetical moves; none may be written to the input.
        var board = Boards.Parse("XX.|OO.|X..");
        var before = board.ToArray();

        ComputerStrategy.ChooseCell(board);

        Assert.Equal(before, board);
    }

    [Fact]
    public void AlwaysChoosesAnEmptyCell_InEveryLegalPosition()
    {
        var positions = 0;
        foreach (var board in LegalPositionsWithOToMove())
        {
            var before = board.ToArray();

            var cell = ComputerStrategy.ChooseCell(board);

            Assert.Null(board[cell]);
            Assert.Equal(before, board);
            positions++;
        }

        Assert.True(positions > 1000, $"Expected to check every legal position, checked {positions}.");
    }

    [Fact]
    public void FullBoard_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ComputerStrategy.ChooseCell(Boards.Parse("XOX|XOO|OXX")));
    }

    /// <summary>Every board with one more X than O, no winner, and at least one empty cell.</summary>
    private static IEnumerable<Player?[]> LegalPositionsWithOToMove()
    {
        for (var code = 0; code < 19683; code++) // 3^9 ways to fill the board
        {
            var board = new Player?[9];
            var rest = code;
            for (var cell = 0; cell < 9; cell++, rest /= 3)
            {
                board[cell] = (rest % 3) switch { 1 => Player.X, 2 => Player.O, _ => null };
            }

            var xs = board.Count(c => c == Player.X);
            var os = board.Count(c => c == Player.O);
            if (xs == os + 1 && GameRules.Evaluate(board).Status == GameStatus.InProgress)
            {
                yield return board;
            }
        }
    }
}
