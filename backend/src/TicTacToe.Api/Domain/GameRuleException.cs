namespace TicTacToe.Api.Domain;

/// <summary>A game command that breaks a rule. Thrown before any state is changed.</summary>
public sealed class GameRuleException(GameErrorCode code, string message) : Exception(message)
{
    public GameErrorCode Code { get; } = code;
}
