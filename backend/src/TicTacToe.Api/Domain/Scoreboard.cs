namespace TicTacToe.Api.Domain;

/// <summary>Counts completed games for one play session.</summary>
public sealed class Scoreboard
{
    public int XWins { get; private set; }

    public int OWins { get; private set; }

    public int Draws { get; private set; }

    public void Record(GameOutcome outcome)
    {
        switch (outcome.Status, outcome.Winner)
        {
            case (GameStatus.Won, Player.X):
                XWins++;
                break;
            case (GameStatus.Won, Player.O):
                OWins++;
                break;
            case (GameStatus.Draw, null):
                Draws++;
                break;
            default:
                throw new ArgumentException("Only a completed game can be recorded.", nameof(outcome));
        }
    }

    public void Reset()
    {
        XWins = 0;
        OWins = 0;
        Draws = 0;
    }
}
