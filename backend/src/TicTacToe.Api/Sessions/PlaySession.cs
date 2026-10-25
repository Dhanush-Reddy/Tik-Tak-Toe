using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Sessions;

/// <summary>
/// An anonymous play session: one scoreboard and exactly one active game. Not thread-safe on its
/// own; <see cref="GameSessionService"/> holds <see cref="Sync"/> around every read and write.
/// </summary>
public sealed class PlaySession(GameMode mode)
{
    public Guid Id { get; } = Guid.NewGuid();

    public Lock Sync { get; } = new();

    public Scoreboard Scoreboard { get; } = new();

    public Game CurrentGame { get; private set; } = new(mode);

    /// <summary>The active game, or <c>GameNotFound</c> for an unknown or retired game ID.</summary>
    public Game ActiveGame(Guid gameId) =>
        CurrentGame.Id == gameId
            ? CurrentGame
            : throw new ResourceNotFoundException(
                ResourceNotFoundException.GameNotFound,
                "This game is not the session's active game. It may have been replaced by Reset Game or a mode change.");

    public void Play(Guid gameId, Player player, int row, int column, int expectedRevision)
    {
        var outcome = ActiveGame(gameId).Play(player, row, column, expectedRevision);

        // Play only succeeds on an in-progress game, so a completed outcome here is this game's one
        // transition to Won or Draw. Later moves and undos are rejected, so it cannot be counted twice.
        if (outcome.IsComplete)
        {
            Scoreboard.Record(outcome);
        }
    }

    public void Undo(Guid gameId, int expectedRevision) => ActiveGame(gameId).Undo(expectedRevision);

    /// <summary>Retires the active game and starts a fresh one in the same mode. The scoreboard is kept.</summary>
    public void ResetGame(Guid gameId) => StartNewGame(ActiveGame(gameId).Mode);

    /// <summary>Retires the active game and starts a fresh one in <paramref name="mode"/>. The scoreboard is kept.</summary>
    public void StartNewGame(GameMode mode) => CurrentGame = new Game(mode);
}
