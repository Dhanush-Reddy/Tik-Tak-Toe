namespace TicTacToe.Api.Domain;

/// <summary>The result of evaluating a board. <see cref="WinningCells"/> holds row-major cell indexes.</summary>
public sealed record GameOutcome(GameStatus Status, Player? Winner, IReadOnlyList<int> WinningCells)
{
    public static readonly GameOutcome InProgress = new(GameStatus.InProgress, null, []);

    public static readonly GameOutcome Draw = new(GameStatus.Draw, null, []);

    public bool IsComplete => Status != GameStatus.InProgress;
}
