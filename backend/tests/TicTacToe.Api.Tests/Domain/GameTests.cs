using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Tests.Domain;

public class GameTests
{
    [Fact]
    public void NewGame_StartsEmptyWithXToMove()
    {
        var game = new Game(GameMode.TwoPlayer);

        Assert.Equal(0, game.Revision);
        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.Equal(GameStatus.InProgress, game.Outcome.Status);
        Assert.All(game.Board(), cell => Assert.Null(cell));
        Assert.Empty(game.Moves);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void ValidMove_RecordsMoveAndSwitchesTurn()
    {
        var game = new Game(GameMode.TwoPlayer);

        var outcome = game.Play(Player.X, row: 1, column: 2, expectedRevision: 0);

        Assert.Equal(GameStatus.InProgress, outcome.Status);
        Assert.Equal(new Move(1, Player.X, 1, 2), Assert.Single(game.Moves));
        Assert.Equal(Player.X, game.Board()[5]);
        Assert.Equal(Player.O, game.CurrentPlayer);
        Assert.Equal(1, game.Revision);
        Assert.True(game.CanUndo);
    }

    [Fact]
    public void Turns_AlternateBetweenXAndO()
    {
        var game = new Game(GameMode.TwoPlayer);

        Moves.Play(game, 0, 4, 8, 2);

        Assert.Equal([Player.X, Player.O, Player.X, Player.O], game.Moves.Select(m => m.Player));
        Assert.Equal([1, 2, 3, 4], game.Moves.Select(m => m.Number));
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    [Theory]
    [InlineData(Player.O, 0, 0, 1, GameErrorCode.CellOccupied)]
    [InlineData(Player.X, 1, 1, 1, GameErrorCode.WrongPlayer)]
    [InlineData(Player.O, 3, 0, 1, GameErrorCode.CellOutOfRange)]
    [InlineData(Player.O, 0, -1, 1, GameErrorCode.CellOutOfRange)]
    [InlineData(Player.O, 1, 1, 0, GameErrorCode.StaleRevision)]
    [InlineData(Player.O, 1, 1, 7, GameErrorCode.StaleRevision)]
    public void InvalidMove_IsRejectedAndLeavesStateUnchanged(Player player, int row, int column, int expectedRevision, GameErrorCode expected)
    {
        var game = new Game(GameMode.TwoPlayer);
        game.Play(Player.X, 0, 0, expectedRevision: 0);
        var before = GameSnapshot.Of(game);

        var error = Assert.Throws<GameRuleException>(() => game.Play(player, row, column, expectedRevision));

        Assert.Equal(expected, error.Code);
        Assert.Equal(before, GameSnapshot.Of(game));
        Assert.Equal(Player.O, game.CurrentPlayer); // the turn did not change
    }

    [Fact]
    public void Win_EndsGameWithWinnerAndNoCurrentPlayer()
    {
        var game = new Game(GameMode.TwoPlayer);

        Moves.Play(game, Lines.XWinsTopRow);

        Assert.Equal(GameStatus.Won, game.Outcome.Status);
        Assert.Equal(Player.X, game.Outcome.Winner);
        Assert.Equal([0, 1, 2], game.Outcome.WinningCells);
        Assert.Null(game.CurrentPlayer);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public void NinthMoveWin_IsWonNotDraw()
    {
        var game = new Game(GameMode.TwoPlayer);

        Moves.Play(game, Lines.NinthMoveDoubleWin);

        Assert.Equal(9, game.Moves.Count);
        Assert.Equal(GameStatus.Won, game.Outcome.Status);
        Assert.Equal(Player.X, game.Outcome.Winner);
        Assert.Equal([0, 1, 2, 4, 6], game.Outcome.WinningCells);
    }

    [Fact]
    public void FullBoardWithoutWinner_IsDraw()
    {
        var game = new Game(GameMode.TwoPlayer);

        Moves.Play(game, Lines.Draw);

        Assert.Equal(GameStatus.Draw, game.Outcome.Status);
        Assert.Null(game.Outcome.Winner);
        Assert.Empty(game.Outcome.WinningCells);
        Assert.Null(game.CurrentPlayer);
    }

    [Fact]
    public void MoveAfterWin_IsRejected()
    {
        var game = new Game(GameMode.TwoPlayer);
        Moves.Play(game, Lines.XWinsTopRow);
        var before = GameSnapshot.Of(game);

        var error = Assert.Throws<GameRuleException>(() => game.Play(Player.O, 2, 2, game.Revision));

        Assert.Equal(GameErrorCode.GameCompleted, error.Code);
        Assert.Equal(before, GameSnapshot.Of(game));
    }

    [Fact]
    public void MoveAfterDraw_IsRejected()
    {
        var game = new Game(GameMode.TwoPlayer);
        Moves.Play(game, Lines.Draw);
        var before = GameSnapshot.Of(game);

        var error = Assert.Throws<GameRuleException>(() => game.Play(Player.X, 0, 0, game.Revision));

        Assert.Equal(GameErrorCode.GameCompleted, error.Code);
        Assert.Equal(before, GameSnapshot.Of(game));
    }

    [Fact]
    public void TwoPlayerUndo_RemovesOnlyLastMoveAndRestoresTurn()
    {
        var game = new Game(GameMode.TwoPlayer);
        Moves.Play(game, 0, 4); // X then O

        game.Undo(expectedRevision: 2);

        Assert.Equal(new Move(1, Player.X, 0, 0), Assert.Single(game.Moves));
        Assert.Null(game.Board()[4]);
        Assert.Equal(Player.O, game.CurrentPlayer); // O's move was removed, so it is O's turn again
        Assert.Equal(3, game.Revision);

        game.Play(Player.O, 1, 1, game.Revision); // the undone cell is playable again
        Assert.Equal(Player.O, game.Board()[4]);
    }

    [Fact]
    public void RepeatedUndo_StopsAtEmptyBoard()
    {
        var game = new Game(GameMode.TwoPlayer);
        Moves.Play(game, 0, 4);

        game.Undo(game.Revision);
        game.Undo(game.Revision);

        Assert.Empty(game.Moves);
        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.False(game.CanUndo);

        var before = GameSnapshot.Of(game);
        var error = Assert.Throws<GameRuleException>(() => game.Undo(game.Revision));
        Assert.Equal(GameErrorCode.NothingToUndo, error.Code);
        Assert.Equal(before, GameSnapshot.Of(game));
    }

    [Fact]
    public void Undo_WithStaleRevision_IsRejected()
    {
        var game = new Game(GameMode.TwoPlayer);
        Moves.Play(game, 0, 4);
        var before = GameSnapshot.Of(game);

        var error = Assert.Throws<GameRuleException>(() => game.Undo(expectedRevision: 1));

        Assert.Equal(GameErrorCode.StaleRevision, error.Code);
        Assert.Equal(before, GameSnapshot.Of(game));
    }

    [Theory]
    [InlineData(GameMode.TwoPlayer)]
    [InlineData(GameMode.Computer)]
    public void UndoAfterCompletion_IsRejected(GameMode mode)
    {
        var game = new Game(mode);
        Moves.Play(game, mode == GameMode.TwoPlayer ? Lines.XWinsTopRow : Lines.ComputerHumanWins);
        Assert.Equal(GameStatus.Won, game.Outcome.Status);
        Assert.False(game.CanUndo);
        var before = GameSnapshot.Of(game);

        var error = Assert.Throws<GameRuleException>(() => game.Undo(game.Revision));

        Assert.Equal(GameErrorCode.GameCompleted, error.Code);
        Assert.Equal(before, GameSnapshot.Of(game));
    }

    [Fact]
    public void ComputerMode_RepliesInSameOperation()
    {
        var game = new Game(GameMode.Computer);

        game.Play(Player.X, 0, 0, expectedRevision: 0);

        Assert.Equal([new Move(1, Player.X, 0, 0), new Move(2, Player.O, 1, 1)], game.Moves);
        Assert.Equal(1, game.Revision); // one command, one revision, even though two moves were added
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    [Fact]
    public void ComputerMode_FollowsPriorities_ThroughAGame()
    {
        var game = new Game(GameMode.Computer);

        Moves.Play(game, 0, 8, 6);

        // O: center (4), then lowest free corner (2), then block X's left column (3).
        Assert.Equal([0, 4, 8, 2, 6, 3], game.Moves.Select(m => m.Cell));
    }

    [Fact]
    public void ComputerMode_NoReplyAfterHumanWin()
    {
        var game = new Game(GameMode.Computer);

        Moves.Play(game, Lines.ComputerHumanWins);

        Assert.Equal(GameStatus.Won, game.Outcome.Status);
        Assert.Equal(Player.X, game.Outcome.Winner);
        Assert.Equal([6, 7, 8], game.Outcome.WinningCells);
        Assert.Equal(7, game.Moves.Count);
        Assert.Equal(new Move(7, Player.X, 2, 1), game.Moves[^1]); // the human's winning move is last
    }

    [Fact]
    public void ComputerMode_NoReplyAfterHumanDraw()
    {
        var game = new Game(GameMode.Computer);

        Moves.Play(game, Lines.ComputerDraw);

        Assert.Equal(GameStatus.Draw, game.Outcome.Status);
        Assert.Equal(9, game.Moves.Count);
        Assert.Equal(Player.X, game.Moves[^1].Player);
    }

    [Fact]
    public void ComputerMode_ComputerCanWin()
    {
        var game = new Game(GameMode.Computer);

        Moves.Play(game, Lines.ComputerWins);

        Assert.Equal(GameStatus.Won, game.Outcome.Status);
        Assert.Equal(Player.O, game.Outcome.Winner);
        Assert.Equal([0, 4, 8], game.Outcome.WinningCells);
        Assert.Equal(Player.O, game.Moves[^1].Player);
    }

    [Fact]
    public void ComputerMode_ClientCannotPlayO()
    {
        var game = new Game(GameMode.Computer);
        game.Play(Player.X, 0, 0, expectedRevision: 0);
        var before = GameSnapshot.Of(game);

        var error = Assert.Throws<GameRuleException>(() => game.Play(Player.O, 2, 2, game.Revision));

        Assert.Equal(GameErrorCode.WrongPlayer, error.Code);
        Assert.Equal(before, GameSnapshot.Of(game));
    }

    [Fact]
    public void ComputerUndo_RemovesHumanAndComputerMovesTogether()
    {
        var game = new Game(GameMode.Computer);
        Moves.Play(game, 0, 8); // X 0, O 4, X 8, O 2

        game.Undo(game.Revision);

        Assert.Equal([new Move(1, Player.X, 0, 0), new Move(2, Player.O, 1, 1)], game.Moves);
        Assert.Null(game.Board()[8]);
        Assert.Null(game.Board()[2]);
        Assert.Equal(Player.X, game.CurrentPlayer);

        game.Undo(game.Revision);
        Assert.Empty(game.Moves);
        Assert.False(game.CanUndo);

        var error = Assert.Throws<GameRuleException>(() => game.Undo(game.Revision));
        Assert.Equal(GameErrorCode.NothingToUndo, error.Code);
    }
}
