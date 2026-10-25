namespace TicTacToe.Api.Domain;

/// <summary>One entry of a game's move history. <see cref="Row"/> and <see cref="Column"/> are zero-based.</summary>
public sealed record Move(int Number, Player Player, int Row, int Column)
{
    public int Cell => GameRules.CellIndex(Row, Column);
}
