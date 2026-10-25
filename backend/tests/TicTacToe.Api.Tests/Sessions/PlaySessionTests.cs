using TicTacToe.Api.Domain;
using TicTacToe.Api.Sessions;

namespace TicTacToe.Api.Tests.Sessions;

public class PlaySessionTests
{
    [Fact]
    public void Win_IncrementsWinnerOnce()
    {
        var session = new PlaySession(GameMode.TwoPlayer);

        Moves.Play(session, Lines.XWinsTopRow);

        AssertScore(session, xWins: 1, oWins: 0, draws: 0);
    }

    [Fact]
    public void OWin_IncrementsOWins()
    {
        var session = new PlaySession(GameMode.TwoPlayer);

        Moves.Play(session, Lines.OWinsMiddleRow);

        AssertScore(session, xWins: 0, oWins: 1, draws: 0);
    }

    [Fact]
    public void Draw_IncrementsDrawsOnce()
    {
        var session = new PlaySession(GameMode.TwoPlayer);

        Moves.Play(session, Lines.Draw);

        AssertScore(session, xWins: 0, oWins: 0, draws: 1);
    }

    [Fact]
    public void ComputerWin_IsScoredForO()
    {
        var session = new PlaySession(GameMode.Computer);

        Moves.Play(session, Lines.ComputerWins);

        AssertScore(session, xWins: 0, oWins: 1, draws: 0);
    }

    [Fact]
    public void InProgressMoves_DoNotScore()
    {
        var session = new PlaySession(GameMode.TwoPlayer);

        Moves.Play(session, 0, 3, 1, 4);

        AssertScore(session, xWins: 0, oWins: 0, draws: 0);
    }

    [Fact]
    public void RejectedMoveAfterWin_DoesNotChangeScoreOrState()
    {
        var session = new PlaySession(GameMode.TwoPlayer);
        Moves.Play(session, Lines.XWinsTopRow);
        var game = session.CurrentGame;
        var before = GameSnapshot.Of(game);

        Assert.Throws<GameRuleException>(() => session.Play(game.Id, Player.O, 2, 2, game.Revision));
        Assert.Throws<GameRuleException>(() => session.Play(game.Id, Player.X, 2, 2, game.Revision));
        Assert.Throws<GameRuleException>(() => session.Undo(game.Id, game.Revision));

        Assert.Equal(before, GameSnapshot.Of(game));
        AssertScore(session, xWins: 1, oWins: 0, draws: 0);
    }

    [Fact]
    public void ResetGame_PreservesScoreAndModeAndClearsGame()
    {
        var session = new PlaySession(GameMode.Computer);
        Moves.Play(session, Lines.ComputerWins);
        var oldGameId = session.CurrentGame.Id;

        session.ResetGame(oldGameId);

        var game = session.CurrentGame;
        Assert.NotEqual(oldGameId, game.Id);
        Assert.Equal(GameMode.Computer, game.Mode);
        Assert.Equal(0, game.Revision);
        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.Equal(GameStatus.InProgress, game.Outcome.Status);
        Assert.Empty(game.Outcome.WinningCells);
        Assert.Empty(game.Moves);
        Assert.All(game.Board(), cell => Assert.Null(cell));
        AssertScore(session, xWins: 0, oWins: 1, draws: 0);
    }

    [Fact]
    public void RetiredGame_CannotBeModified()
    {
        var session = new PlaySession(GameMode.TwoPlayer);
        var oldGame = session.CurrentGame;
        Moves.Play(session, 0);
        session.ResetGame(oldGame.Id);
        var replacement = GameSnapshot.Of(session.CurrentGame);

        // Late requests still carrying the old game ID (and its last revision) must not touch the new game.
        AssertGameNotFound(() => session.Play(oldGame.Id, Player.O, 1, 1, expectedRevision: 1));
        AssertGameNotFound(() => session.Play(oldGame.Id, Player.X, 1, 1, expectedRevision: 0));
        AssertGameNotFound(() => session.Undo(oldGame.Id, expectedRevision: 1));
        AssertGameNotFound(() => session.ResetGame(oldGame.Id));

        Assert.Equal(replacement, GameSnapshot.Of(session.CurrentGame));
    }

    [Fact]
    public void StartNewGame_InOtherMode_KeepsScoreboard()
    {
        var session = new PlaySession(GameMode.TwoPlayer);
        Moves.Play(session, Lines.XWinsTopRow);
        var oldGameId = session.CurrentGame.Id;

        session.StartNewGame(GameMode.Computer);

        Assert.Equal(GameMode.Computer, session.CurrentGame.Mode);
        Assert.Empty(session.CurrentGame.Moves);
        AssertGameNotFound(() => session.ActiveGame(oldGameId));
        AssertScore(session, xWins: 1, oWins: 0, draws: 0);
    }

    [Fact]
    public void ResetScoreboard_KeepsBoardAndDoesNotRecount()
    {
        var session = new PlaySession(GameMode.TwoPlayer);
        Moves.Play(session, Lines.XWinsTopRow);
        var before = GameSnapshot.Of(session.CurrentGame);

        session.Scoreboard.Reset();

        AssertScore(session, xWins: 0, oWins: 0, draws: 0);
        Assert.Equal(before, GameSnapshot.Of(session.CurrentGame));
        Assert.Equal(Player.X, session.CurrentGame.Outcome.Winner);

        // The finished game cannot be completed (and so counted) a second time.
        var game = session.CurrentGame;
        Assert.Throws<GameRuleException>(() => session.Play(game.Id, Player.O, 2, 2, game.Revision));
        AssertScore(session, xWins: 0, oWins: 0, draws: 0);
    }

    [Fact]
    public void ResetScoreboard_ThenNewResult_CountsNormally()
    {
        var session = new PlaySession(GameMode.TwoPlayer);
        Moves.Play(session, Lines.XWinsTopRow);
        session.Scoreboard.Reset();

        session.ResetGame(session.CurrentGame.Id);
        Moves.Play(session, Lines.Draw);

        AssertScore(session, xWins: 0, oWins: 0, draws: 1);
    }

    private static void AssertScore(PlaySession session, int xWins, int oWins, int draws)
    {
        Assert.Equal(
            (xWins, oWins, draws),
            (session.Scoreboard.XWins, session.Scoreboard.OWins, session.Scoreboard.Draws));
    }

    private static void AssertGameNotFound(Action action)
    {
        var error = Assert.Throws<ResourceNotFoundException>(action);
        Assert.Equal(ResourceNotFoundException.GameNotFound, error.Code);
    }
}
