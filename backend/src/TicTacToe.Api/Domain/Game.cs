namespace TicTacToe.Api.Domain;

/// <summary>
/// One game of Tic Tac Toe. The move list is the only stored game state: the board, current
/// player, status, winner, winning cells and undo availability are all derived from it, so they
/// cannot disagree. Not thread-safe; <c>GameSessionService</c> serializes access per session.
/// </summary>
public sealed class Game
{
    private readonly List<Move> _moves = [];

    public Game(GameMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown game mode.");
        }

        Mode = mode;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public GameMode Mode { get; }

    /// <summary>
    /// Starts at 0 and increases by one per successful move or undo command (a human move plus the
    /// computer's reply is one command). Clients send it back as <c>expectedRevision</c> so a stale
    /// or duplicated command is rejected instead of becoming a new turn.
    /// </summary>
    public int Revision { get; private set; }

    public IReadOnlyList<Move> Moves => _moves.AsReadOnly();

    public GameOutcome Outcome => GameRules.Evaluate(Board());

    /// <summary>The player to move, or null once the game is won or drawn.</summary>
    public Player? CurrentPlayer => Outcome.IsComplete ? null : NextPlayer;

    public bool CanUndo => !Outcome.IsComplete && _moves.Count >= UndoStep;

    /// <summary>X moves first, so X is to move whenever an even number of moves has been made.</summary>
    private Player NextPlayer => _moves.Count % 2 == 0 ? Player.X : Player.O;

    /// <summary>Two Player undo removes one move; Computer undo removes the human/computer pair.</summary>
    private int UndoStep => Mode == GameMode.Computer ? 2 : 1;

    /// <summary>A new array each call, rebuilt from the move history.</summary>
    public Player?[] Board()
    {
        var board = new Player?[GameRules.CellCount];
        foreach (var move in _moves)
        {
            board[move.Cell] = move.Player;
        }

        return board;
    }

    /// <summary>
    /// Applies a client's move and, in Computer mode, the computer's reply when the game is still in
    /// progress. Every check runs before the first change, so a rejected move changes nothing.
    /// </summary>
    /// <returns>The outcome after the command.</returns>
    public GameOutcome Play(Player player, int row, int column, int expectedRevision)
    {
        EnsureRevision(expectedRevision);

        if (Outcome.IsComplete)
        {
            throw new GameRuleException(GameErrorCode.GameCompleted, "The game is over. Start a new game to keep playing.");
        }

        if (Mode == GameMode.Computer && player == Player.O)
        {
            throw new GameRuleException(GameErrorCode.WrongPlayer, "In Computer mode you play X; the computer plays O.");
        }

        if (player != NextPlayer)
        {
            throw new GameRuleException(GameErrorCode.WrongPlayer, $"It is {NextPlayer}'s turn.");
        }

        if (!GameRules.IsOnBoard(row, column))
        {
            throw new GameRuleException(GameErrorCode.CellOutOfRange, "Row and column must each be between 0 and 2.");
        }

        if (Board()[GameRules.CellIndex(row, column)] is not null)
        {
            throw new GameRuleException(GameErrorCode.CellOccupied, "That cell is already taken.");
        }

        Append(player, row, column);

        // The human move is evaluated first: if it won or filled the board, the computer does not move.
        if (Mode == GameMode.Computer && !Outcome.IsComplete)
        {
            var cell = ComputerStrategy.ChooseCell(Board());
            Append(Player.O, cell / GameRules.Size, cell % GameRules.Size);
        }

        Revision++;
        return Outcome;
    }

    /// <summary>
    /// Removes the last move (Two Player) or the last human/computer pair (Computer). Completed
    /// games are final (assessment Option A), so undo is rejected after a win or draw.
    /// </summary>
    public void Undo(int expectedRevision)
    {
        EnsureRevision(expectedRevision);

        if (Outcome.IsComplete)
        {
            throw new GameRuleException(GameErrorCode.GameCompleted, "Completed games are final, so undo is disabled. Start a new game instead.");
        }

        if (_moves.Count < UndoStep)
        {
            throw new GameRuleException(GameErrorCode.NothingToUndo, "There is no move to undo.");
        }

        // An in-progress Computer game always ends with a complete X/O pair, because the computer
        // replies within the same command as the human move.
        _moves.RemoveRange(_moves.Count - UndoStep, UndoStep);
        Revision++;
    }

    private void EnsureRevision(int expectedRevision)
    {
        if (expectedRevision != Revision)
        {
            throw new GameRuleException(
                GameErrorCode.StaleRevision,
                $"The game has changed since revision {expectedRevision}; it is now at revision {Revision}. Nothing was applied.");
        }
    }

    private void Append(Player player, int row, int column) =>
        _moves.Add(new Move(_moves.Count + 1, player, row, column));
}
