using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Tests.Domain;

public class GameRulesTests
{
    [Theory]
    [InlineData(0, 1, 2)] // top row
    [InlineData(3, 4, 5)] // middle row
    [InlineData(6, 7, 8)] // bottom row
    [InlineData(0, 3, 6)] // left column
    [InlineData(1, 4, 7)] // middle column
    [InlineData(2, 5, 8)] // right column
    [InlineData(0, 4, 8)] // main diagonal
    [InlineData(2, 4, 6)] // anti-diagonal
    public void EveryWinningLine_IsDetected(int a, int b, int c)
    {
        // X on the line plus two O moves elsewhere: the position right after X's winning fifth move.
        var board = new Player?[9];
        int[] line = [a, b, c];
        foreach (var cell in line)
        {
            board[cell] = Player.X;
        }

        foreach (var cell in Enumerable.Range(0, 9).Except(line).Take(2))
        {
            board[cell] = Player.O;
        }

        var outcome = GameRules.Evaluate(board);

        Assert.Equal(GameStatus.Won, outcome.Status);
        Assert.Equal(Player.X, outcome.Winner);
        Assert.Equal(line, outcome.WinningCells);
    }

    [Fact]
    public void OWin_IsDetected()
    {
        var outcome = GameRules.Evaluate(Boards.Parse("XX.|OOO|X.."));

        Assert.Equal(GameStatus.Won, outcome.Status);
        Assert.Equal(Player.O, outcome.Winner);
        Assert.Equal([3, 4, 5], outcome.WinningCells);
    }

    [Fact]
    public void MoveCompletingTwoLines_HighlightsUnion()
    {
        // Top row and anti-diagonal completed by the same X move at cell 2.
        var outcome = GameRules.Evaluate(Boards.Parse("XXX|OXO|XOO"));

        Assert.Equal(Player.X, outcome.Winner);
        Assert.Equal([0, 1, 2, 4, 6], outcome.WinningCells);
    }

    [Fact]
    public void FullBoardWithLine_IsWinNotDraw()
    {
        var outcome = GameRules.Evaluate(Boards.Parse("XOX|XOO|XXO"));

        Assert.Equal(GameStatus.Won, outcome.Status);
        Assert.Equal(Player.X, outcome.Winner);
        Assert.Equal([0, 3, 6], outcome.WinningCells);
    }

    [Fact]
    public void FullBoardWithoutLine_IsDraw()
    {
        var outcome = GameRules.Evaluate(Boards.Parse("XOX|XOO|OXX"));

        Assert.Equal(GameStatus.Draw, outcome.Status);
        Assert.Null(outcome.Winner);
        Assert.Empty(outcome.WinningCells);
    }

    [Fact]
    public void PartialBoardWithoutLine_IsInProgress()
    {
        var outcome = GameRules.Evaluate(Boards.Parse("X..|.O.|..X"));

        Assert.Equal(GameStatus.InProgress, outcome.Status);
        Assert.Null(outcome.Winner);
        Assert.Empty(outcome.WinningCells);
    }
}
