using System.ComponentModel.DataAnnotations;
using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Contracts;

// Requests carry only the client's intent. Unknown properties (for example a "board" or "winner")
// are rejected, and `required` makes a missing property a 400 instead of a silent default.

public sealed record NewGameRequest
{
    [EnumDataType(typeof(GameMode))]
    public required GameMode Mode { get; init; }
}

public sealed record MoveRequest
{
    [EnumDataType(typeof(Player))]
    public required Player Player { get; init; }

    /// <summary>Zero-based row, 0–2.</summary>
    [Range(0, GameRules.Size - 1, ErrorMessage = "row must be between 0 and 2 (zero-based).")]
    public required int Row { get; init; }

    /// <summary>Zero-based column, 0–2.</summary>
    [Range(0, GameRules.Size - 1, ErrorMessage = "column must be between 0 and 2 (zero-based).")]
    public required int Column { get; init; }

    /// <summary>The game revision the client last saw.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "expectedRevision cannot be negative.")]
    public required int ExpectedRevision { get; init; }
}

public sealed record UndoRequest
{
    /// <summary>The game revision the client last saw.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "expectedRevision cannot be negative.")]
    public required int ExpectedRevision { get; init; }
}
