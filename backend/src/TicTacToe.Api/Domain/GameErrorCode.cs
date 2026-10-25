namespace TicTacToe.Api.Domain;

/// <summary>Why a game command was rejected. The names are returned to clients as the error <c>code</c>.</summary>
public enum GameErrorCode
{
    CellOutOfRange,
    CellOccupied,
    WrongPlayer,
    GameCompleted,
    NothingToUndo,
    StaleRevision,
}
