namespace TicTacToe.Api.Sessions;

/// <summary>An unknown session, or a game ID that is not the session's active game.</summary>
public sealed class ResourceNotFoundException(string code, string message) : Exception(message)
{
    public const string SessionNotFound = nameof(SessionNotFound);
    public const string GameNotFound = nameof(GameNotFound);

    public string Code { get; } = code;
}
