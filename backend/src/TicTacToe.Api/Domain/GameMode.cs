namespace TicTacToe.Api.Domain;

public enum GameMode
{
    /// <summary>Both X and O are played by people at the same browser.</summary>
    TwoPlayer,

    /// <summary>The person plays X; the server plays O.</summary>
    Computer,
}
