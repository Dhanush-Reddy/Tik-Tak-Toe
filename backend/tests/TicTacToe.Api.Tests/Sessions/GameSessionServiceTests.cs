using TicTacToe.Api.Domain;
using TicTacToe.Api.Sessions;

namespace TicTacToe.Api.Tests.Sessions;

public class GameSessionServiceTests
{
    private readonly GameSessionService _service = new();

    [Fact]
    public void Sessions_HaveIndependentScoreboardsAndGames()
    {
        var a = _service.CreateSession(GameMode.TwoPlayer);
        var b = _service.CreateSession(GameMode.TwoPlayer);

        PlayAll(a.SessionId, a.GameId, Lines.XWinsTopRow);

        Assert.Equal(1, _service.GetScoreboard(a.SessionId).XWins);
        Assert.Equal(0, _service.GetScoreboard(b.SessionId).XWins);
        Assert.Empty(_service.GetCurrentGame(b.SessionId).Moves);

        // A game ID only resolves inside its own session.
        var error = Assert.Throws<ResourceNotFoundException>(() => _service.Play(b.SessionId, a.GameId, Player.X, 2, 2, 0));
        Assert.Equal(ResourceNotFoundException.GameNotFound, error.Code);
    }

    [Fact]
    public void UnknownSession_IsNotFound()
    {
        var error = Assert.Throws<ResourceNotFoundException>(() => _service.GetCurrentGame(Guid.NewGuid()));

        Assert.Equal(ResourceNotFoundException.SessionNotFound, error.Code);
    }

    [Fact]
    public void ReadingState_DoesNotChangeScoreboard()
    {
        var state = _service.CreateSession(GameMode.TwoPlayer);
        PlayAll(state.SessionId, state.GameId, Lines.XWinsTopRow);

        for (var i = 0; i < 3; i++)
        {
            _service.GetCurrentGame(state.SessionId);
            _service.GetGame(state.SessionId, state.GameId);
            _service.GetScoreboard(state.SessionId);
        }

        Assert.Equal(new(1, 0, 0), _service.GetScoreboard(state.SessionId));
    }

    [Fact]
    public void ConcurrentDuplicateMoves_OnlyOneSucceeds()
    {
        var state = _service.CreateSession(GameMode.Computer);
        var successes = 0;
        var staleRejections = 0;

        // 32 copies of "X plays at revision 0", on different cells, arriving at the same time.
        Parallel.For(0, 32, i =>
        {
            try
            {
                _service.Play(state.SessionId, state.GameId, Player.X, (i % 9) / 3, i % 3, expectedRevision: 0);
                Interlocked.Increment(ref successes);
            }
            catch (GameRuleException e) when (e.Code == GameErrorCode.StaleRevision)
            {
                Interlocked.Increment(ref staleRejections);
            }
        });

        var game = _service.GetCurrentGame(state.SessionId);
        Assert.Equal(1, successes);
        Assert.Equal(31, staleRejections);
        Assert.Equal(1, game.Revision);
        Assert.Equal(2, game.Moves.Count); // one human move and its computer reply, nothing else
    }

    private void PlayAll(Guid sessionId, Guid gameId, int[] cells)
    {
        foreach (var cell in cells)
        {
            var game = _service.GetGame(sessionId, gameId);
            _service.Play(sessionId, gameId, game.CurrentPlayer!.Value, cell / 3, cell % 3, game.Revision);
        }
    }
}
