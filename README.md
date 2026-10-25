# Tic Tac Toe — Angular + .NET

A browser-based Tic Tac Toe game. The Angular frontend renders whatever the .NET Web API returns,
and the API owns all game state: board, turn, validation, result, winning cells, move history,
computer moves, and scoreboard. Everything is kept in memory and runs locally, with no Docker or
database.

- **Specification:** [docs/SPEC.md](docs/SPEC.md): requirements, decisions, state transitions, API contract, and a requirement-to-test checklist
- **Review guide:** [docs/REVIEW_GUIDE.md](docs/REVIEW_GUIDE.md): demo script, code walkthrough, and likely questions
- **AI workflow:** [docs/AI_WORKFLOW.md](docs/AI_WORKFLOW.md): prompts, what was generated, corrections, and verification

## Tech stack and prerequisites

| Part | Technology | Version used and verified |
|------|------------|---------------------------|
| Backend | ASP.NET Core Web API (controllers), C#, .NET 10 (LTS) | .NET SDK 10.0.113, runtime 10.0.13 |
| Backend tests | xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing 10.0.13 | |
| Frontend | Angular 21 (standalone components, signals, zoneless), TypeScript 5.9 | Angular 21.2.25, CLI 21.2.28 |
| Frontend tests | Vitest 4 with jsdom, through `ng test` | Vitest 4.1.12 |
| Runtime | Node.js `^20.19.0 \|\| ^22.12.0 \|\| >=24.0.0` (Angular 21's supported range) | Node 22.22.0, npm 10.9.4 |
| Storage | In-memory (`ConcurrentDictionary` of sessions) | — |

You need the **.NET 10 SDK** (any 10.0.1xx or later; `backend/global.json` rolls forward) and
**Node.js** in the range above. Nothing else is required: no global Angular CLI, no HTTPS dev
certificate, no database.

> Angular 21 was chosen over the newer Angular 22 because Angular 22 requires Node ≥ 22.22.3 and the
> build environment had Node 22.22.0. Both backend and frontend use only stable packages.

## Run it locally

Use two terminals from the repository root.

**1. Backend (http://localhost:5080)**

```bash
cd backend
dotnet run --project src/TicTacToe.Api
```

It listens on `http://localhost:5080` (set in `src/TicTacToe.Api/Properties/launchSettings.json`). In
Development it also serves the OpenAPI document at http://localhost:5080/openapi/v1.json.

**2. Frontend (http://localhost:4200)**

```bash
cd frontend
npm ci
npm start
```

Open **http://localhost:4200**.

**Proxy and CORS.** The browser only ever talks to the Angular dev server on port 4200. The dev server
forwards every `/api/...` request to `http://localhost:5080` (see `frontend/proxy.conf.json`, wired up
in `angular.json` → `serve.options.proxyConfig`). Because the page and the API share one origin from
the browser's point of view, the API needs **no CORS policy**, and none is configured. If you change
the API port, change it in both `launchSettings.json` and `proxy.conf.json`.

Use `npm ci` (installs exactly what `package-lock.json` lists). `npm install` without a lockfile
crashes on npm 10.9.x with an internal npm error (`Cannot read properties of null (reading
'edgesOut')`). That is an npm bug, not a dependency conflict: npm 11 resolves the same `package.json`
cleanly. `npm ci` works on npm 10.9.4 (verified).

## Run the tests

```bash
cd backend
dotnet test          # 101 tests: rules, computer strategy, game/session transitions, HTTP contract

cd ../frontend
npm ci               # if not already installed
npm test             # 13 tests (Vitest, single run)
npm run build        # production build into frontend/dist/
```

`npx ng test` (without `--watch=false`) runs the frontend tests in watch mode.

## Features

- 3×3 board. Empty cells are clickable buttons; a played cell shows X or O and is locked for that game.
- **Two Player** mode, and **Play Against Computer** mode (you are X, the server plays O immediately
  after your move, in the same request).
- Turn indicator, win detection (rows, columns, diagonals) with highlighted winning cells, and draw detection.
- Move history (move number, player, "Row r, Column c").
- **Undo Last Move**: removes one move in Two Player mode and your move plus the computer's reply in
  Computer mode. Disabled when there is nothing to undo and after a win or draw.
- **Reset Game**: fresh game in the same mode, X to move, scoreboard kept.
- **Scoreboard**: X wins, O wins, and draws for this browser tab's session, served by the backend,
  plus a separate **Reset Scoreboard**.
- Mode switching starts a fresh game and keeps the scoreboard (stated next to the control).
- Refreshing the page resumes the same session. If the backend has restarted, a new session starts
  with a clear message.
- Accessible: real buttons with labels like "Row 2, column 3, empty", visible keyboard focus, a live
  status region, and X/O distinguished by text as well as colour. Works from laptop width down to
  phone width.

## API summary

All routes are under `http://localhost:5080`. JSON is camelCase; enums are strings (`"X"`/`"O"`,
`"TwoPlayer"`/`"Computer"`, `"InProgress"`/`"Won"`/`"Draw"`). **Rows and columns are zero-based**
(`0..2`). `board` and `winningCells` use the cell index `row * 3 + column` (`0..8`). The UI shows
one-based "Row 1, Column 1".

| Method | Route | Body | Success |
|--------|-------|------|---------|
| POST | `/api/sessions` | `{"mode":"TwoPlayer"}` | 201 game state (new session + first game) |
| GET  | `/api/sessions/{sessionId}` | — | 200 state of the session's active game |
| POST | `/api/sessions/{sessionId}/games` | `{"mode":"Computer"}` | 201 new game (used to switch mode) |
| GET  | `/api/sessions/{sessionId}/games/{gameId}` | — | 200 game state |
| POST | `/api/sessions/{sessionId}/games/{gameId}/moves` | `{"player":"X","row":0,"column":0,"expectedRevision":0}` | 200 game state |
| POST | `/api/sessions/{sessionId}/games/{gameId}/undo` | `{"expectedRevision":1}` | 200 game state |
| POST | `/api/sessions/{sessionId}/games/{gameId}/reset` | — | 200 state of the **new** game |
| GET  | `/api/sessions/{sessionId}/scoreboard` | — | 200 `{"xWins":0,"oWins":0,"draws":0}` |
| POST | `/api/sessions/{sessionId}/scoreboard/reset` | — | 200 scoreboard |

These map one-to-one onto the assessment's suggested `/api/games/...` and `/api/scoreboard/...`
routes, nested under a session because the scoreboard belongs to a session (see
[Design decisions](#design-decisions)). The full contract, including every error code, is in
[docs/SPEC.md §4](docs/SPEC.md#4-rest-api-contract). `backend/src/TicTacToe.Api/TicTacToe.Api.http`
has ready-to-run requests.

Every game response contains `sessionId`, `gameId`, `revision`, `mode`, `status`, `currentPlayer`
(`null` when finished), `winner`, `winningCells`, `board`, `moves`, `canUndo`, and `scoreboard`.

### Examples (captured from the running API)

Create a session in Computer mode:

```http
POST /api/sessions
Content-Type: application/json

{"mode":"Computer"}
```

```http
HTTP/1.1 201 Created
Location: http://localhost:5080/api/sessions/7e3931d7-98d6-4532-9a5d-e5bc9a39ab8a

{"sessionId":"7e3931d7-98d6-4532-9a5d-e5bc9a39ab8a","gameId":"83e1158b-6000-4f94-a3ec-68c5ea804a57",
 "revision":0,"mode":"Computer","status":"InProgress","currentPlayer":"X","winner":null,"winningCells":[],
 "board":[null,null,null,null,null,null,null,null,null],"moves":[],"canUndo":false,
 "scoreboard":{"xWins":0,"oWins":0,"draws":0}}
```

Play X in the top-left corner. The response already contains the computer's reply in the centre, and
the revision went up once:

```http
POST /api/sessions/7e3931d7-…/games/83e1158b-…/moves
Content-Type: application/json

{"player":"X","row":0,"column":0,"expectedRevision":0}
```

```http
HTTP/1.1 200 OK

{"sessionId":"7e3931d7-98d6-4532-9a5d-e5bc9a39ab8a","gameId":"83e1158b-6000-4f94-a3ec-68c5ea804a57",
 "revision":1,"mode":"Computer","status":"InProgress","currentPlayer":"X","winner":null,"winningCells":[],
 "board":["X",null,null,null,"O",null,null,null,null],
 "moves":[{"number":1,"player":"X","row":0,"column":0},{"number":2,"player":"O","row":1,"column":1}],
 "canUndo":true,"scoreboard":{"xWins":0,"oWins":0,"draws":0}}
```

**Stale-state conflict.** The same client sends another move still claiming revision 0, as a
duplicated click or delayed retry would:

```http
POST /api/sessions/7e3931d7-…/games/83e1158b-…/moves
Content-Type: application/json

{"player":"X","row":2,"column":2,"expectedRevision":0}
```

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.10",
 "title":"The request conflicts with the current game state","status":409,
 "detail":"The game has changed since revision 0; it is now at revision 1. Nothing was applied.",
 "traceId":"00-ddc526a2a307ecd765e4886137854398-0d3d83199e72bb16-00","code":"StaleRevision"}
```

**Validation failure.** Row 3 is outside the board:

```http
POST /api/sessions/7e3931d7-…/games/83e1158b-…/moves
Content-Type: application/json

{"player":"X","row":3,"column":0,"expectedRevision":1}
```

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1",
 "title":"One or more validation errors occurred.","status":400,
 "errors":{"row":["row must be between 0 and 2 (zero-based)."]},
 "traceId":"00-f229ce184bff18be90a7917c9992f438-40a6ae0f5ea767c2-00","code":"ValidationFailed"}
```

An unknown enum value is rejected the same way (`{"mode":"Chess"}` → `"errors":{"$.mode":["Expected
one of: TwoPlayer, Computer."]}`).

| Status | `code` | Meaning |
|--------|--------|---------|
| 400 | `ValidationFailed` | malformed or invalid request (shape, field values, unknown fields such as `board`) |
| 404 | `SessionNotFound` / `GameNotFound` | unknown or expired session; game ID that is not the session's active game |
| 409 | `StaleRevision`, `GameCompleted`, `WrongPlayer`, `CellOccupied`, `NothingToUndo` | valid request that conflicts with the current game state; nothing is changed |

## Design decisions

The assessment's requirements are summarized in [docs/SPEC.md §1](docs/SPEC.md#1-explicit-requirements-from-the-assessment).
The choices below resolve what it leaves open.

- **The backend is the only source of truth.** The server stores one ordered move history per game and
  derives everything else from it: board, turn, status, winner, winning cells, and `canUndo`. The
  browser sends only intent (player, row, column, expected revision); a request that includes a `board`
  or `winner` field is rejected. Angular holds presentation state only and never applies a rule or an
  optimistic update.
- **Pure rules, separate from storage and HTTP.** `Domain/` (`GameRules`, `ComputerStrategy`, `Game`,
  `Scoreboard`) has no ASP.NET dependencies. `Sessions/` stores sessions and serializes access.
  `Controllers/` only translate HTTP. There is no repository layer, mediator, or CQRS, since none of
  them would pay for itself at this size.
- **Undo: Option A.** Undo is disabled after a win or draw, and the backend also rejects it
  (`409 GameCompleted`). Completed results are final, so the scoreboard never has to be reversed. The
  assessment explicitly allows this.
- **Computer turn = one operation.** The human move is validated, applied, and evaluated. Only if the
  game is still in progress does the computer reply. Both happen under one lock and one revision
  bump. The strategy follows the assessment's priorities exactly (win, block, center, corner, any),
  with the lowest cell index breaking ties. It is deliberately not minimax and is beatable.
- **Exactly-once scoring.** The scoreboard changes only inside a successful move that takes a game
  from `InProgress` to `Won`/`Draw`. Moves and undo on a finished game are rejected and GETs never
  write, so no second transition can happen.
- **Concurrency.** Each session has its own lock, and every command, including the response
  snapshot, runs inside it. The `ConcurrentDictionary` alone would not make a multi-step command
  atomic. Each game also has a `revision`; move and undo requests must carry the revision the client
  saw, so a duplicated or late command gets `409 StaleRevision` instead of becoming a new turn. The
  UI never replays a failed command. It reloads state and lets you act again.
- **Strict input.** Enums accept only their exact names (the built-in converter also accepted
  `"x"` and `"TwoPlayer, Computer"`), numbers must be JSON numbers, and unknown properties are
  rejected. Errors are `application/problem+json` with a stable `code` and no stack traces.

## Clarifications and assumptions

- **Scoreboard scope = an anonymous play session.** `POST /api/sessions` creates a session with one
  scoreboard and one active game. The browser keeps only the session ID, in `sessionStorage`, so a
  refresh resumes that tab's session while the backend is running. Another tab or browser gets its
  own session and score. This is local demo state, not authentication.
- **"Reset Game … start a fresh game session"** is read as: retire the current game and create a new
  game ID in the same session and mode, keeping the scoreboard. The old game ID then returns
  `404 GameNotFound`, so a delayed request for it cannot change the new game.
- **Switching mode** starts a fresh game in the chosen mode and keeps the scoreboard. Moves from the
  old game are never reinterpreted under the new mode.
- **Reset Scoreboard** clears only the counters. The current board, history, and result stay as they
  are, and a finished game is not counted again.
- **The human is always X and moves first in Computer mode**, and the computer is always O. A client
  that tries to play O in Computer mode gets `409 WrongPlayer`.
- **Coordinates** are zero-based in the API and one-based in the UI and history.
- **The win check comes before the draw check**, so a ninth move that completes a line is a win. If
  one move completes two lines, all of their cells are highlighted.
- **HTTP semantics:** 400 for malformed or invalid requests, 404 for unknown or retired IDs, and
  409 for well-formed requests that conflict with the game's current state.

## Known limitations

- State is in memory only. Restarting the API clears every session; the UI detects this and starts a
  new session with a message. Sessions are never evicted, so memory grows with the number of
  sessions. That is fine for a local demo but not for a long-running server.
- A session ID is a bearer identifier stored in `sessionStorage`, not a user identity. There is no
  authentication by design.
- The computer opponent follows the five required priorities and can be beaten. It does not look ahead.
- The frontend is meant to run through `ng serve` and its proxy. The production build (`npm run
  build`) is verified to compile, but no production web host or CORS setup is included.
- `npm audit` reports advisories in `undici` 7.29.1. That is a build-time dependency that
  `@angular/build` 21.2.28 pins exactly; it is not shipped to the browser. It was left as is rather
  than overriding a dependency the Angular toolchain pins; a future Angular patch release should
  pick up the fix.
- `npm install` on npm 10.9.x hits an npm bug (see [Run it locally](#run-it-locally)); `npm ci` is
  unaffected.
- There are no automated browser end-to-end tests in the repository. The full UI was exercised
  against the real API with a Playwright script during development (see
  [docs/AI_WORKFLOW.md](docs/AI_WORKFLOW.md)).

## Future improvements

- Idle-session expiry (for example, a sliding 2-hour timeout) and a cap on the number of sessions.
- Optional persistence (SQLite) behind the session store, so restarts keep scores.
- A small Playwright end-to-end suite in CI, plus a GitHub Actions workflow running `dotnet test`,
  `npm test`, and `npm run build`.
- Serve the built Angular app from the API (or a reverse proxy) for a single-command production run.
- A confirmation before switching mode in the middle of a game.

## AI tools and prompt summary

The application was built with **Claude Code** (Anthropic's coding agent) from two inputs: the
assessment document, and a written implementation brief that set priorities, behaviour decisions
(Option A undo, session-scoped scoreboard, computer priorities, revisions), and verification
requirements. The agent wrote the specification, backend, frontend, tests, and documentation, and
ran every check listed above. [docs/AI_WORKFLOW.md](docs/AI_WORKFLOW.md) records the prompts, what was
generated, the corrections made along the way, the verification performed, and what still needs the
candidate's personal review.

## Repository layout

```
backend/
  global.json, Directory.Build.props, TicTacToe.slnx
  src/TicTacToe.Api/
    Domain/        pure rules: Game, GameRules, ComputerStrategy, Scoreboard, enums, Move
    Sessions/      PlaySession (scoreboard + active game), GameSessionService (store + locking)
    Contracts/     request/response DTOs, strict enum converter, OpenAPI enum schemas
    Controllers/   Sessions, Games, Scoreboard
    Errors/        exception → problem+json mapping
    Program.cs     JSON options, validation responses, error handling, OpenAPI
  tests/TicTacToe.Api.Tests/
    Domain/ Sessions/ Api/
frontend/
  proxy.conf.json  /api → http://localhost:5080
  src/app/
    api/           typed API client, models, error mapping
    game-store.ts  signals store: server state, pending/error state, recovery
    board/ scoreboard/ move-history/   presentational components
    app.*          game screen: status, mode, controls, layout
docs/
  SPEC.md, REVIEW_GUIDE.md, AI_WORKFLOW.md
```
