# Tic Tac Toe — Specification

This document turns the assessment ("Round 2 – Problem Statement") into a small, checkable
specification. It distinguishes **explicit assessment requirements** (`R*`) from
**implementation decisions** (`D*`) that resolve ambiguity. It is kept in sync with the code;
file references point at the implementation.

## 1. Explicit requirements (from the assessment)

| ID  | Requirement |
|-----|-------------|
| R1  | Angular + TypeScript frontend, .NET Web API backend, REST, in-memory storage, runs locally. |
| R2  | 3×3 board; empty cells clickable; a played cell shows X/O and stays locked for the current game. |
| R3  | Players X and O alternate after every valid move; invalid moves do not change the turn; the UI shows whose turn it is. |
| R4  | Detect wins on any row, column or diagonal: show winner, highlight winning cells, block further moves, update scoreboard. |
| R5  | Detect a draw when all 9 cells are filled with no winner: show draw, block further moves, update scoreboard. |
| R6  | Reset Game: clear board, history, and result; X to move; start a fresh game session; keep the scoreboard. |
| R7  | Move history: move number, player, and cell position (for example "Row 1, Column 1"), updated after each valid move. |
| R8  | Undo Last Move: restore board, turn, and status, and keep history accurate. Two Player removes one move; Computer removes the computer's move and the human's previous move. Disabled when there is nothing to undo. |
| R9  | Session-level scoreboard (X wins, O wins, draws) served by the backend. Updates exactly once per completed game; unchanged by Reset Game; separate Reset Scoreboard. |
| R10 | Modes: Two Player, and Play Against Computer (human X, computer O). The computer moves automatically, makes only valid moves, never moves after completion, and uses priorities win → block → center → corner → any. |
| R11 | Backend owns game state, move history, status, and scoreboard, and exposes REST operations: create, get, move, undo, reset, get scoreboard, reset scoreboard. |
| R12 | Game state response: game ID, board, current player, mode, status (`InProgress`/`Won`/`Draw`), winner, winning cells, move history, scoreboard (or a way to retrieve it). |
| R13 | A move request includes game ID, player, and row/column (or cell index). The backend rejects moves outside the board, on occupied cells, after completion, and by the wrong player. |
| R14 | UI shows the board, current player, mode, winner/draw message, highlighted winning cells, history, scoreboard, and the Reset Game, Undo Last Move, and Reset Scoreboard buttons. The UI renders backend state and is usable on a laptop. |
| R15 | Scoreboard and Undo: choose Option A (Undo disabled after completion) or Option B, and document the choice. |
| R16 | Tests for: valid move, invalid move, turn switching, row/column/diagonal win, draw, reset, undo in both modes, scoreboard update, computer selection, and move after completion. |
| R17 | README covering overview, stack, features, run and test instructions, API, AI usage, decisions, assumptions, limitations, and future work. |

## 2. Implementation decisions

| ID  | Decision |
|-----|----------|
| D1  | **Backend authority.** The server holds the move history and derives board, turn, status, winner, winning cells, and `canUndo` from it (`Domain/Game.cs`). The browser sends only *intent* (player, row, column, expected revision). Unknown JSON properties such as `board` or `winner` are rejected with 400. |
| D2  | **Play session = scoreboard scope.** `POST /api/sessions` creates an anonymous session holding one scoreboard and exactly one *active* game. The browser keeps only the session ID in `sessionStorage`, so a refresh resumes that tab's session. Separate tabs and sessions have separate scores. This is not authentication. |
| D3  | **Undo Option A.** Undo is disabled after a win or draw (`canUndo = false`), and the backend rejects it with `409 GameCompleted`. Completed results are final, so the scoreboard never has to be reversed. |
| D4  | **Two Player undo** removes exactly the last move. That player is to move again and the cell becomes playable. |
| D5  | **Computer undo** removes the last human/computer *pair* from an in-progress game; X is to move. An in-progress computer game always has an even number of moves because the computer replies in the same operation. |
| D6  | **Computer turn in one operation.** `POST …/moves` validates and applies X's move, evaluates it, and only if the game is still in progress appends O's reply. Both happen under one lock and one revision increment. There is no artificial delay. |
| D7  | **Computer priorities**, exactly: (1) a cell that wins for O, (2) a cell that blocks an immediate X win, (3) center, (4) a corner, (5) any empty cell. Ties within a priority go to the lowest row-major cell index. This is not minimax and is not unbeatable (`Domain/ComputerStrategy.cs`). |
| D8  | **Reset Game** retires the session's active game and creates a new game ID in the same session and mode. X moves first; the board, history, result, and highlights are empty. The old game ID no longer resolves (`404 GameNotFound`), so late requests for it cannot touch the replacement. This is our reading of "start a fresh game session". |
| D9  | **Switching mode** = `POST /api/sessions/{id}/games` with the new mode. It starts a fresh game and keeps the scoreboard. Existing moves are never reinterpreted under a different mode. The UI states this next to the mode control. |
| D10 | **Reset Scoreboard** zeroes only the session counters. The board, history, mode, and status are unchanged. A completed game is not counted again, because scoring happens only on the transition to completion (D12). |
| D11 | **Coordinates.** The API is **zero-based**: `row` and `column` are in `0..2`. `board` and `winningCells` use the row-major cell index `row * 3 + column` (`0..8`). The UI and history display one-based "Row r, Column c". Error messages avoid coordinates. |
| D12 | **Exactly-once scoring.** The scoreboard is updated only inside a successful move that moves the game from `InProgress` to `Won`/`Draw`. Moves on completed games are rejected, undo after completion is rejected, and GETs never mutate, so no second transition is possible (`Sessions/PlaySession.cs`). |
| D13 | **Terminal state.** The win check runs before the draw check, so a ninth move that completes a line is a win. A completed game has `currentPlayer = null`. If one move completes two lines, `winningCells` is the sorted union. |
| D14 | **Concurrency.** Each session has its own lock. Every mutation, including human move + computer reply + score update, runs inside it, as does building the response snapshot. Sessions are held in a `ConcurrentDictionary`, which alone would not make multi-step operations atomic. |
| D15 | **Optimistic concurrency.** Each game has a `revision` that starts at 0 and increments once per successful move or undo. Move and undo requests must carry `expectedRevision`; a mismatch returns `409 StaleRevision` and changes nothing. The UI never replays a failed mutation. It reloads authoritative state and lets the user act again. |
| D16 | **Validation before mutation.** Every check runs before the first state change, so a rejected command leaves the history, revision, and score unchanged. |
| D17 | **Errors** are RFC 9457 `application/problem+json` with a stable `code` and no stack traces. 400 = malformed or invalid request; 404 = unknown session or unknown/retired game; 409 = valid request that conflicts with the current game state. |
| D18 | **Ports and proxy.** The API runs at `http://localhost:5080`. The Angular dev server runs at `http://localhost:4200` and proxies `/api` to the API, so the browser is same-origin and no CORS policy is needed. |
| D19 | **Expired state.** Restarting the backend clears all sessions. The UI then gets `404 SessionNotFound`, starts a new session, and says so. |

## 3. State model and transitions

Stored per session (`Sessions/PlaySession.cs`): `Scoreboard {xWins, oWins, draws}` and the active
`Game {id, mode, revision, moves[]}`. Everything else is derived from `moves` in
`Domain/Game.cs` and `Domain/GameRules.cs`:

- `board[i]` = the player of the move at cell `i`.
- `status`/`winner`/`winningCells` = `GameRules.Evaluate(board)` (wins checked first, then a full board → `Draw`).
- `currentPlayer` = `null` if the game is complete, otherwise X if the move count is even, else O.
- `canUndo` = `InProgress` and the move count ≥ the undo step (1 in Two Player, 2 in Computer).

| From | Command | Guard (all checked before mutating) | Effect | Score |
|------|---------|-------------------------------------|--------|-------|
| — | Create session(mode) | valid mode | new session, empty scoreboard, new game (rev 0) | — |
| InProgress | Move(p, r, c, rev) | rev matches; game in progress; computer mode ⇒ p = X; p = current player; cell empty | append move; in Computer mode, if still in progress, append O's reply; rev + 1 | +1 for the winner or draw if this command completed the game |
| InProgress | Undo(rev) | rev matches; move count ≥ step | remove the last 1 (Two Player) or 2 (Computer) moves; rev + 1 | unchanged |
| Won/Draw | Move / Undo | — | rejected `409 GameCompleted` (or `StaleRevision` if rev is old) | unchanged |
| any | Reset Game | game ID is the active game | replace with new game (new ID, same mode, rev 0) | unchanged |
| any | Start game(mode) | valid mode | replace with new game (new ID, given mode, rev 0) | unchanged |
| any | Reset Scoreboard | session exists | counters → 0; game untouched | cleared |
| any | GET game / session / scoreboard | — | none | none |

## 4. REST API contract

Base URL: `http://localhost:5080`. JSON uses camelCase. Enums are strings: `Player` `X|O`, `GameMode`
`TwoPlayer|Computer`, `GameStatus` `InProgress|Won|Draw`. Route IDs are GUIDs.

| Method | Route | Body | Success | Assessment equivalent |
|--------|-------|------|---------|-----------------------|
| POST | `/api/sessions` | `{ "mode": "TwoPlayer" }` | 201 `GameState` (+ `Location`) | session creation (D2) |
| GET  | `/api/sessions/{sessionId}` | — | 200 `GameState` of the active game | resume / refresh |
| POST | `/api/sessions/{sessionId}/games` | `{ "mode": "Computer" }` | 201 `GameState` | `POST /api/games` |
| GET  | `/api/sessions/{sessionId}/games/{gameId}` | — | 200 `GameState` | `GET /api/games/{id}` |
| POST | `/api/sessions/{sessionId}/games/{gameId}/moves` | `{ "player": "X", "row": 0, "column": 2, "expectedRevision": 0 }` | 200 `GameState` | `POST /api/games/{id}/moves` |
| POST | `/api/sessions/{sessionId}/games/{gameId}/undo` | `{ "expectedRevision": 2 }` | 200 `GameState` | `POST /api/games/{id}/undo` |
| POST | `/api/sessions/{sessionId}/games/{gameId}/reset` | — | 200 `GameState` of the **new** game | `POST /api/games/{id}/reset` |
| GET  | `/api/sessions/{sessionId}/scoreboard` | — | 200 `Scoreboard` | `GET /api/scoreboard` |
| POST | `/api/sessions/{sessionId}/scoreboard/reset` | — | 200 `Scoreboard` | `POST /api/scoreboard/reset` |

Routes are nested under the session because the scoreboard belongs to a session (D2). The session
lock also guards its game. The OpenAPI document is served at `/openapi/v1.json` in Development.

`GameState`:

```json
{
  "sessionId": "guid", "gameId": "guid", "revision": 3,
  "mode": "TwoPlayer", "status": "InProgress",
  "currentPlayer": "O", "winner": null, "winningCells": [],
  "board": ["X", null, null, null, "O", null, null, null, "X"],
  "moves": [ { "number": 1, "player": "X", "row": 0, "column": 0 } ],
  "canUndo": true,
  "scoreboard": { "xWins": 0, "oWins": 0, "draws": 0 }
}
```

Errors (`application/problem+json`, always with `status`, `title`, `detail` or `errors`, `code`, and `traceId`):

| Status | `code` | When |
|--------|--------|------|
| 400 | `ValidationFailed` | malformed or empty JSON, missing field, unknown field (such as `board`), enum value that is not an exact name (`"Z"`, `"x"`, `1`, `"TwoPlayer, Computer"`), number sent as a string, row/column outside 0–2, negative revision |
| 404 | `SessionNotFound` | unknown or expired session ID |
| 404 | `GameNotFound` | the game ID is not the session's active game (unknown or retired by reset or mode switch) |
| 409 | `StaleRevision` | `expectedRevision` ≠ current revision |
| 409 | `GameCompleted` | move or undo after a win or draw |
| 409 | `WrongPlayer` | not that player's turn, or a client plays O in Computer mode |
| 409 | `CellOccupied` | the target cell is already taken |
| 409 | `NothingToUndo` | no move (Two Player) or move pair (Computer) to undo |

Check order for a move: request shape (400) → session (404) → active game (404) → revision → completed → player → cell.

## 5. Requirement-to-test checklist

Backend tests are in `backend/tests/TicTacToe.Api.Tests` (101 cases); frontend tests are `frontend/src/app/**/*.spec.ts` (13 cases).

| Requirement / risk | Test(s) |
|--------------------|---------|
| Valid move, turn switching (R3) | `GameTests.ValidMove_RecordsMoveAndSwitchesTurn`, `GameTests.Turns_AlternateBetweenXAndO`, `GameApiTests.Move_ReturnsUpdatedStateWithHistory` |
| Invalid moves rejected; history, revision, and turn unchanged (R3, R13, D16) | `GameTests.InvalidMove_IsRejectedAndLeavesStateUnchanged` (occupied, wrong player, two out-of-range cases, two stale-revision cases), `GameApiTests.InvalidMoveRequest_Returns400AndChangesNothing` (12 malformed bodies), `GameApiTests.OccupiedCell_Returns409`, `GameApiTests.WrongPlayer_Returns409` |
| All 8 lines win; double-line union (R4, D13) | `GameRulesTests.EveryWinningLine_IsDetected` (8 cases), `GameRulesTests.OWin_IsDetected`, `GameRulesTests.MoveCompletingTwoLines_HighlightsUnion` |
| Ninth-move win is not a draw (D13) | `GameRulesTests.FullBoardWithLine_IsWinNotDraw`, `GameTests.NinthMoveWin_IsWonNotDraw` |
| Draw (R5) | `GameRulesTests.FullBoardWithoutLine_IsDraw`, `GameTests.FullBoardWithoutWinner_IsDraw` |
| Move after completion (R4, R5, R13) | `GameTests.MoveAfterWin_IsRejected`, `GameTests.MoveAfterDraw_IsRejected`, `GameApiTests.MoveAndUndoAfterCompletion_Return409` |
| Two Player undo (R8, D4) | `GameTests.TwoPlayerUndo_RemovesOnlyLastMoveAndRestoresTurn`, `GameTests.RepeatedUndo_StopsAtEmptyBoard`, `GameApiTests.Undo_TwoPlayer_RemovesLastMove` |
| Computer undo (R8, D5) | `GameTests.ComputerUndo_RemovesHumanAndComputerMovesTogether`, `GameApiTests.Undo_Computer_RemovesMovePair` |
| Undo after completion rejected (D3) | `GameTests.UndoAfterCompletion_IsRejected` (both modes), `GameApiTests.MoveAndUndoAfterCompletion_Return409` |
| Computer priorities and tie-breaks (R10, D7) | `ComputerStrategyTests.WinsInsteadOfBlocking`, `BlocksInsteadOfTakingCenter`, `TakesCenterWhenNothingToWinOrBlock`, `TakesLowestFreeCornerWhenCenterIsTaken`, `TakesFirstCornerWhenAllCornersAreFree`, `TakesLowestEmptyCellWhenCenterAndCornersAreTaken`, `ChoosesLowestWinningCellWhenSeveralWin`, `ChoosesLowestBlockingCellWhenXHasSeveralThreats`, `GameTests.ComputerMode_FollowsPriorities_ThroughAGame` |
| Computer only makes valid moves and never mutates its input | `ComputerStrategyTests.ChooseCell_DoesNotModifyBoard`, `ComputerStrategyTests.AlwaysChoosesAnEmptyCell_InEveryLegalPosition` (every legal position with O to move) |
| Computer replies in one operation; never after completion; client cannot play O (R10, D6) | `GameTests.ComputerMode_RepliesInSameOperation`, `GameTests.ComputerMode_NoReplyAfterHumanWin`, `GameTests.ComputerMode_NoReplyAfterHumanDraw`, `GameTests.ComputerMode_ComputerCanWin`, `GameTests.ComputerMode_ClientCannotPlayO`, `GameApiTests.ComputerMode_MoveResponseIncludesComputerReply`, `GameApiTests.ComputerMode_PlayingAsO_Returns409` |
| Scoreboard exactly once (R9, D12) | `PlaySessionTests.Win_IncrementsWinnerOnce`, `OWin_IncrementsOWins`, `Draw_IncrementsDrawsOnce`, `ComputerWin_IsScoredForO`, `InProgressMoves_DoNotScore`, `RejectedMoveAfterWin_DoesNotChangeScoreOrState`, `GameSessionServiceTests.ReadingState_DoesNotChangeScoreboard`, `GameApiTests.Win_IsReportedAndScoredOnce`, `GameApiTests.Get_DoesNotChangeScoreboard` |
| Reset Game (R6, D8) | `PlaySessionTests.ResetGame_PreservesScoreAndModeAndClearsGame`, `PlaySessionTests.RetiredGame_CannotBeModified`, `GameApiTests.ResetGame_ReturnsFreshGameAndKeepsScore`, `GameApiTests.RetiredGameId_Returns404` |
| Mode switch (D9) | `PlaySessionTests.StartNewGame_InOtherMode_KeepsScoreboard`, `GameApiTests.StartNewGame_SwitchesModeAndKeepsScore` |
| Reset Scoreboard (R9, D10) | `PlaySessionTests.ResetScoreboard_KeepsBoardAndDoesNotRecount`, `PlaySessionTests.ResetScoreboard_ThenNewResult_CountsNormally`, `GameApiTests.ResetScoreboard_ClearsCountsAndKeepsGame` |
| Session isolation (D2) | `GameSessionServiceTests.Sessions_HaveIndependentScoreboardsAndGames`, `GameApiTests.Sessions_HaveSeparateScoreboards`, `GameApiTests.UnknownSession_Returns404`, `GameSessionServiceTests.UnknownSession_IsNotFound` |
| Stale and duplicate commands; atomicity (D14, D15) | `GameSessionServiceTests.ConcurrentDuplicateMoves_OnlyOneSucceeds` (32 parallel requests, 1 applied), `GameTests.Undo_WithStaleRevision_IsRejected`, `GameApiTests.StaleRevision_Returns409AndChangesNothing` |
| HTTP contract: shape, enums as strings, 201/Location, problem+json (D11, D17) | `GameApiTests.CreateSession_Returns201WithInitialState`, `GameApiTests.InvalidMode_Returns400`, every `AssertProblem` call in `GameApiTests` |
| UI renders server state (R14) | `app.spec.ts`: "creates a session and renders the empty game…", "renders a finished game as returned…", "switches mode through the API…", "undo, reset game, and reset scoreboard…" |
| UI locks controls while pending; one request per click; occupied and finished cells unplayable | `app.spec.ts`: "sends one move with the expected revision and locks the controls…", "does not send a move for an occupied cell", "renders a finished game…" (click on empty cell sends nothing); `board.spec.ts` |
| UI errors and recovery (D15, D19) | `app.spec.ts`: "keeps the last confirmed state and offers a reload when the API is unreachable", "shows a rejected move and reloads server state instead of retrying it", "starts a new session with a notice when the stored session no longer exists" |
| Accessibility labels, winning highlight | `board.spec.ts`: "labels every cell…", "marks the winning cells visibly and in their labels" |

Outside the automated suites, the full UI was also driven in a real browser against the real API: 31 checks
covering every flow above, plus keyboard play, page refresh, a backend restart, the backend going down, and a
stale second tab. `docs/AI_WORKFLOW.md` describes that run; its script is not part of the repository.
