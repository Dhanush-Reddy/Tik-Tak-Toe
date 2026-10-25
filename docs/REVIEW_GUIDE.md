# Review guide

A short guide for presenting and defending the implementation. File references are relative to
`backend/src/TicTacToe.Api/` (backend) or `frontend/src/app/` (frontend) unless shown in full.

## 1. Five-minute demo

Start the API (`cd backend && dotnet run --project src/TicTacToe.Api`) and the UI
(`cd frontend && npm ci && npm start`), then open http://localhost:4200.

| Time | Do | Point out |
|------|----|-----------|
| 0:00 | Show the empty screen. | The status line says "Player X's turn". Board, mode, scoreboard, and history are all on one laptop screen. The session was created by `POST /api/sessions`. |
| 0:30 | **Two Player.** Play Row 1 Col 1 (X), Row 2 Col 1 (O), Row 1 Col 2 (X), Row 2 Col 2 (O), Row 1 Col 3 (X). | Turns alternate and history grows. "Player X wins!": the three cells are highlighted, X wins = 1, the board is locked, and **Undo is disabled** (Option A; the hint below the buttons says why). |
| 1:15 | Click **Reset game**. | Board, history, and result are cleared, X is to move, and **the score stays at 1**. This is a new game ID in the same session. |
| 1:35 | Play X, then O, then click **Undo last move**. | Only O's move is removed, and "Player O's turn" returns. Click Undo again: the board is empty and Undo is disabled. |
| 2:05 | Click **Play against computer**. | The hint says a mode change starts a new game and keeps the scoreboard. The score is still 1. |
| 2:20 | Play Row 1 Col 1. | O answers in the centre in the **same** response; the history shows both moves, with O marked "computer". Click **Undo**: both moves disappear and it is X's turn. |
| 2:50 | Play Row 1 Col 1, Row 1 Col 2, Row 3 Col 1, Row 3 Col 3. | O takes the centre, then **blocks** Row 1 Col 3, then **blocks** Row 2 Col 1. On the last move X threatens the bottom row, but O **wins** the middle row instead of blocking: win comes before block. "The computer wins (O)". |
| 3:40 | Click **Reset scoreboard**. | All counters go to 0, and the finished board and history stay exactly as they were. |
| 4:00 | Refresh the browser. | The same session and board come back (the session ID is in `sessionStorage`). |
| 4:20 | Optional: stop the API and click a cell. | The red banner reads "Can't reach the game server…" and the last confirmed board stays. Restart the API and click **Reload game**: a new session starts and a notice explains why. |

## 2. One request, end to end (a Computer-mode move)

Browser → `POST /api/sessions/{s}/games/{g}/moves` with `{"player":"X","row":0,"column":0,"expectedRevision":0}`

1. **UI guard.** The click calls `GameStore.play` (`game-store.ts:55`). It ignores the click if a
   request is pending, the game is over, or the cell is taken, then sends the current player,
   row/column, and `state.revision`. `run()` (`game-store.ts:120`) sets the pending state, which
   locks the board and buttons.
2. **Binding and validation.** In `Program.cs:61–65`, JSON accepts enums only by exact name, numbers
   only as numbers, and no unknown properties. `Contracts/Requests.cs` makes every field `required`
   and range-checked. Any failure returns 400 `ValidationFailed` before our code runs
   (`Program.cs:26`).
3. **Controller.** `GamesController.Move` (`Controllers/GamesController.cs:27`) only forwards the
   values to the service.
4. **Session lookup and lock.** `GameSessionService.WithSession` (`Sessions/GameSessionService.cs:70`)
   finds the session (otherwise 404 `SessionNotFound`) and holds `lock (session.Sync)` (line 79)
   for the whole command, including building the response.
5. **Active game.** `PlaySession.Play` (`Sessions/PlaySession.cs:27`) resolves the game through
   `ActiveGame` (line 20); a retired or foreign ID gives 404 `GameNotFound`.
6. **Rules.** `Game.Play` (`Domain/Game.cs:65`) checks, in order: revision → game not finished → O
   not sent in Computer mode → right player's turn → on the board → cell empty. Each failed check
   throws before any change. Then it appends X's move (line 94). **Only if the game is still in
   progress** (line 97) does it ask `ComputerStrategy.ChooseCell` for O's cell and append it. It
   increments `Revision` once (line 103) and returns the new outcome.
7. **Score.** Back in `PlaySession.Play`, if that outcome is `Won` or `Draw`, the scoreboard records it
   (line 35).
8. **Response.** `ToResponse` (`GameSessionService.cs:85`) derives board, status, winner, winning
   cells, current player, and `canUndo` from the move history, and attaches the scoreboard, all from
   one locked snapshot.
9. **Errors.** `ApiExceptionFilter` (`Errors/ApiExceptionFilter.cs:15`) turns `GameRuleException`
   into 409 (or 400 for out-of-range cells) and `ResourceNotFoundException` into 404, as
   problem+json with a `code`. Anything unexpected goes to `UseExceptionHandler` (`Program.cs:46`):
   a generic 500 with no stack trace.
10. **Render.** `accept()` (`game-store.ts:136`) stores the response in a signal, and the
    components re-render from it. On an error, `recover()` (`game-store.ts:142`) keeps the last
    confirmed state. After a 409 it re-reads state with a GET, after a 404 it reloads or starts a new
    session, and on a network failure it offers **Reload game**. It never replays the failed POST.

## 3. Key mechanisms

### Board state is derived, not stored
`Game` stores only `_moves` and `Revision` (`Domain/Game.cs`). The board is rebuilt from the moves
(`Board()`, line 49). Status, winner, and winning cells come from `GameRules.Evaluate`
(`Domain/GameRules.cs:24`). `CurrentPlayer` (line 38) is X after an even number of moves and `null`
once the game is over. `CanUndo` is line 40. There is no stored "winner" or "isOver" flag that could
disagree with the board. Evaluation checks all eight lines **before** checking for a full board
(lines 44 and 56), so a ninth move that completes a line is a win. When one move completes two
lines, the winning cells are their union.

### Undo, including the computer pair
`UndoStep` (`Domain/Game.cs:46`) is 1 in Two Player mode and 2 in Computer mode, and `Undo` (line 111)
removes that many moves from the end (line 127). After that, everything derived (board, turn,
status) is correct automatically. Why removing two is always right in Computer mode: the computer
replies inside the same command as the human move, so an in-progress Computer game always ends with
an X/O pair, and X is always to move. Undo is rejected after a win or draw (Option A), and with
`NothingToUndo` when fewer than `UndoStep` moves exist.

### Computer priorities
`ComputerStrategy.ChooseCell` (`Domain/ComputerStrategy.cs:14`) is five lines that read like the
assessment (lines 24–28): a winning cell for O, else a cell blocking X, else the centre, else a
corner, else any empty cell. Each step scans cells in ascending order, so ties go to the lowest
index. `CompletesLine` (line 35) asks "would this cell complete a line for this player?" by reading
the other two cells of each line. It never places a hypothetical piece, so the input board cannot
be mutated. A test confirms this for every legal position.

### Exactly-once scoring
Scoring happens in one place, `PlaySession.Play` (`Sessions/PlaySession.cs:27–36`), after a
**successful** move whose outcome is `Won` or `Draw`. `Game.Play` throws `GameCompleted` on a
finished game, so a game can only make that transition once. Undo after completion is rejected,
so a result can't be undone and replayed. GET endpoints never write. Reset Scoreboard zeroes the
counters but leaves the finished game finished, so nothing is recounted. Reset Game and a mode
change create a new game, and only that new game can produce the next result.

### Session scope
`POST /api/sessions` creates a `PlaySession` (`Sessions/PlaySession.cs`): one `Scoreboard` plus
exactly one `CurrentGame`. Reset Game and a mode change replace `CurrentGame` (lines 42 and 45), and
the old game ID simply stops resolving. The browser keeps only the session ID in `sessionStorage`
(`game-store.ts:176`), so a refresh resumes the session while separate tabs stay separate. The store
is a singleton dictionary (`Program.cs:12`), so restarting the API clears it and the UI reports that.

### Concurrency
Two mechanisms solve two different problems:

- **Atomicity:** the per-session lock (`GameSessionService.cs:79`). Without it, two requests could
  interleave between "human move appended", "computer reply appended", and "score recorded". The
  `ConcurrentDictionary` (line 14) only makes the lookup safe. Different sessions never block each
  other.
- **Staleness:** the revision. The lock serializes two identical "X plays at revision 0" requests,
  but both would still be legal moves on their own. The revision check (`Game.cs:67`) makes the
  second one a `409 StaleRevision`. The test `ConcurrentDuplicateMoves_OnlyOneSucceeds` fires 32
  of them in parallel and expects exactly one to apply.

## 4. Likely panel questions

**Why can't the frontend just compute the winner?** The assessment makes the backend the source of
truth, and computing it twice invites disagreement. Angular renders `status`, `winner`,
`winningCells`, `currentPlayer`, and `canUndo` exactly as returned and sends only intent. The API
rejects a request carrying `board` or `winner`.

**Your routes differ from the suggested `/api/games/{id}`. Why?** The scoreboard has to belong to
something, and the assessment says "session-level". Nesting games and the scoreboard under
`/api/sessions/{id}` makes that ownership explicit, and the session's lock then naturally guards
its game. Each suggested route has a one-to-one equivalent (table in the README). The assessment
explicitly allows different route names.

**Why Option A for undo?** It keeps a finished result final, so the scoreboard never needs a
compensating decrement. It is the simpler invariant: the score changes only on the transition to
complete. The assessment allows either option.

**How do you know the score can't be counted twice?** See [Exactly-once scoring](#exactly-once-scoring).
Tests cover repeated GETs, rejected moves and undo after a win, a scoreboard reset after a finished
game, and a mutation check (making the score count on later reads breaks two tests).

**What happens if I double-click a cell?** In the UI, the second click is ignored because a request
is pending and the board is locked. Even if two requests reached the API, the second would carry a
stale revision and get a 409 without changing anything.

**Why both a lock and a revision?** They solve different problems; see
[Concurrency](#concurrency).

**Why a revision in the body instead of ETag / If-Match?** That would work equally well. A body
field is easier to see in the contract and in the tests, and it needs no header handling in the
client. Switching to ETag would be a small change in the controller.

**Why 409 rather than 400 or 422 for an occupied cell?** 400 is used for requests that are invalid
whatever the state (bad JSON, out-of-range row). 409 is for valid requests that conflict with the
resource's current state (cell taken, wrong turn, game over, stale revision). 422 would also be
defensible. What matters is that it is consistent, documented, and carries a machine-readable `code`.

**Why exceptions for rule violations?** Every check runs before any change, so throwing can't
leave partial state. An MVC exception filter maps them to 409/404 in one place, without error-level
logging. A `Result` type would be the alternative; for a handful of rules the exception keeps
`Game.Play` linear and readable.

**Why does the computer move in the same request?** The assessment says it moves "automatically
after the human move". Doing it in the same locked command means no one can see or act on a state
where it is O's turn in Computer mode, and the response is complete in one round trip. The human
move is evaluated first, so a winning or board-filling human move gets no reply.

**Is the computer unbeatable?** No, and it isn't meant to be. It follows the five required
priorities and does not look ahead. The demo sequence above wins for O only because X plays badly;
a human can force wins with fork setups.

**Why derive the board from the history every time?** There is a single source of truth, and undo
becomes "remove the last N moves". Rebuilding nine cells is trivial work.

**What happens when the backend restarts?** All sessions are gone. The next request returns
`404 SessionNotFound`. The UI starts a new session and shows a notice that the old one was lost
(`game-store.ts:145`).

**How would you take this to production?** Expire idle sessions, persist sessions (SQLite, or Redis
if there are several instances), serve the built frontend from the API or a reverse proxy, add CI
and a Playwright smoke test, and treat the session ID as a real credential (HttpOnly cookie) if
scores mattered.

**Why no CORS?** The Angular dev server proxies `/api` to the API (`frontend/proxy.conf.json`), so
the browser sees a single origin. Opening the API to other origins would only add attack surface.

**Why Angular 21 and .NET 10?** .NET 10 is the current LTS. Angular 22 requires a newer Node patch
than the environment had, and Angular 21 is the supported LTS line for that Node. Both are stable
releases.

**Accessibility?** Cells are real `<button>`s labelled "Row 2, column 3, empty" (or "…, X, winning
cell"). Unplayable cells use `aria-disabled` so keyboard focus is not lost after a move
(`board/board.ts:38`, `board/board.html:11`). The status line is a `role="status"` live region,
errors use `role="alert"`, focus is always visible, and X and O differ by letter as well as colour.

**How was it tested?** Unit tests on rules and transitions (fast, exhaustive where cheap: all 8
lines and every legal computer position), in-memory HTTP tests for the real contract, and frontend
tests against a mocked backend. Mutation checks showed the tests fail when key rules break. A
real-browser run then exercised everything against the real API, including from a clean clone. See
[AI_WORKFLOW.md §6](AI_WORKFLOW.md#6-verification-performed).

**What did AI generate, and what did you change?** Answer from your own experience. The factual
generation log is [AI_WORKFLOW.md](AI_WORKFLOW.md); it lists the corrections made during
generation and the parts marked for your personal review.

## 5. Files to know, in reading order

1. `docs/SPEC.md`: decisions D1–D19 and the transition table.
2. `backend/src/TicTacToe.Api/Domain/Game.cs`: the rules for one game.
3. `backend/src/TicTacToe.Api/Domain/GameRules.cs` and `Domain/ComputerStrategy.cs`.
4. `backend/src/TicTacToe.Api/Sessions/PlaySession.cs` and `Sessions/GameSessionService.cs`.
5. `backend/src/TicTacToe.Api/Program.cs` and `Errors/ApiExceptionFilter.cs`.
6. `frontend/src/app/game-store.ts`, then `app.html` and `board/board.ts`.
7. Tests: `backend/tests/TicTacToe.Api.Tests/Domain/GameTests.cs`, `Sessions/PlaySessionTests.cs`,
   `Api/GameApiTests.cs`, and `frontend/src/app/app.spec.ts`.
