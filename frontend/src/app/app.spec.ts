import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from './app';
import { GameState } from './api/game.models';
import { gameState, played } from './testing/game-state.fixture';

describe('App (game screen)', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(App);
  });

  afterEach(() => http.verify()); // fails the test if any unexpected request was sent

  const page = () => fixture.nativeElement as HTMLElement;
  const cells = () => Array.from(page().querySelectorAll<HTMLButtonElement>('app-board button'));
  const button = (text: string) =>
    Array.from(page().querySelectorAll<HTMLButtonElement>('button')).find(
      (b) => b.textContent?.trim() === text,
    )!;
  const text = (selector: string) => page().querySelector(selector)?.textContent?.trim() ?? '';

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  /** Starts the page and answers the session-creation request with `state`. */
  async function startWith(state: GameState): Promise<void> {
    fixture.detectChanges();
    http.expectOne({ method: 'POST', url: '/api/sessions' }).flush(state);
    await settle();
  }

  function problem(req: TestRequest, status: number, code: string, detail: string): void {
    req.flush({ status, title: 'Rejected', detail, code }, { status, statusText: 'Error' });
  }

  it('creates a session and renders the empty game returned by the API', async () => {
    fixture.detectChanges();
    const req = http.expectOne({ method: 'POST', url: '/api/sessions' });
    expect(req.request.body).toEqual({ mode: 'TwoPlayer' });
    req.flush(gameState());
    await settle();

    expect(text('[data-testid=status]')).toBe("Player X's turn");
    expect(cells()).toHaveLength(9);
    expect(cells()[0].getAttribute('aria-label')).toBe('Row 1, column 1, empty');
    expect(cells()[5].getAttribute('aria-label')).toBe('Row 2, column 3, empty');
    expect(text('app-move-history')).toContain('No moves yet.');
    expect(text('[data-testid=x-wins]')).toBe('0');
    expect(button('Undo last move').disabled).toBe(true);
    expect(sessionStorage.getItem('tic-tac-toe.sessionId')).toBe('session-1');
  });

  it('renders a finished game as returned: winner, highlighted cells, history, score, locked board', async () => {
    await startWith(
      gameState({
        ...played(0, 3, 1, 4, 2),
        status: 'Won',
        winner: 'X',
        currentPlayer: null,
        winningCells: [0, 1, 2],
        scoreboard: { xWins: 1, oWins: 0, draws: 0 },
      }),
    );

    expect(text('[data-testid=status]')).toBe('Player X wins!');
    expect(
      cells()
        .filter((c) => c.classList.contains('winning'))
        .map((c) => cells().indexOf(c)),
    ).toEqual([0, 1, 2]);
    expect(cells()[0].getAttribute('aria-label')).toBe('Row 1, column 1, X, winning cell');
    expect(cells().every((c) => c.getAttribute('aria-disabled') === 'true')).toBe(true);
    expect(page().querySelectorAll('app-move-history tbody tr')).toHaveLength(5);
    expect(text('app-move-history tbody tr:first-child')).toContain('Row 1, Column 1');
    expect(text('[data-testid=x-wins]')).toBe('1');
    expect(button('Undo last move').disabled).toBe(true);
    expect(button('Reset game').disabled).toBe(false);

    cells()[8].click(); // an empty cell of a finished game sends nothing
    await settle();
  });

  it('sends one move with the expected revision and locks the controls until the server answers', async () => {
    await startWith(gameState({ ...played(0, 4), canUndo: true }));

    cells()[5].click();
    cells()[5].click(); // a rapid second click must not send a second request
    await settle();

    const req = http.expectOne({
      method: 'POST',
      url: '/api/sessions/session-1/games/game-1/moves',
    });
    expect(req.request.body).toEqual({ player: 'X', row: 1, column: 2, expectedRevision: 2 });
    expect(cells().every((c) => c.getAttribute('aria-disabled') === 'true')).toBe(true);
    expect(button('Undo last move').disabled).toBe(true);
    expect(button('Reset game').disabled).toBe(true);
    expect(button('Play against computer').disabled).toBe(true);
    expect(button('Reset scoreboard').disabled).toBe(true);

    req.flush(gameState({ ...played(0, 4, 5), currentPlayer: 'O', canUndo: true }));
    await settle();

    expect(cells()[5].getAttribute('aria-label')).toBe('Row 2, column 3, X');
    expect(text('[data-testid=status]')).toBe("Player O's turn");
    expect(button('Undo last move').disabled).toBe(false);
  });

  it('does not send a move for an occupied cell', async () => {
    await startWith(gameState({ ...played(0), currentPlayer: 'O', canUndo: true }));

    expect(cells()[0].getAttribute('aria-disabled')).toBe('true');
    cells()[0].click();
    await settle();
  });

  it('keeps the last confirmed state and offers a reload when the API is unreachable', async () => {
    await startWith(gameState({ ...played(0), currentPlayer: 'O', canUndo: true }));

    cells()[4].click();
    await settle();
    http
      .expectOne({ method: 'POST', url: '/api/sessions/session-1/games/game-1/moves' })
      .error(new ProgressEvent('error'));
    await settle();

    expect(text('[role=alert]')).toContain("Can't reach the game server");
    expect(cells()[0].textContent?.trim()).toBe('X'); // last confirmed board is still shown
    expect(cells()[4].textContent?.trim()).toBe('');
    expect(text('[data-testid=status]')).toBe("Player O's turn");

    button('Reload game').click(); // nothing is retried automatically; reloading is the person's choice
    await settle();
    http
      .expectOne({ method: 'GET', url: '/api/sessions/session-1' })
      .flush(gameState({ ...played(0), currentPlayer: 'O' }));
    await settle();

    expect(page().querySelector('[role=alert]')).toBeNull();
  });

  it('shows a rejected move and reloads server state instead of retrying it', async () => {
    await startWith(gameState({ ...played(0), currentPlayer: 'O', canUndo: true }));

    cells()[4].click();
    await settle();
    problem(
      http.expectOne({ method: 'POST', url: '/api/sessions/session-1/games/game-1/moves' }),
      409,
      'StaleRevision',
      'The game has changed since revision 1; it is now at revision 2. Nothing was applied.',
    );
    await settle();
    // The follow-up is a read of the current state, never a second move.
    http
      .expectOne({ method: 'GET', url: '/api/sessions/session-1' })
      .flush(gameState({ ...played(0, 8), canUndo: true }));
    await settle();

    expect(text('[role=alert]')).toContain('nothing was applied');
    expect(cells()[8].textContent?.trim()).toBe('O');
    expect(cells()[4].textContent?.trim()).toBe('');
    expect(text('[data-testid=status]')).toBe("Player X's turn");
  });

  it('starts a new session with a notice when the stored session no longer exists', async () => {
    sessionStorage.setItem('tic-tac-toe.sessionId', 'expired-session');
    fixture.detectChanges();

    problem(
      http.expectOne({ method: 'GET', url: '/api/sessions/expired-session' }),
      404,
      'SessionNotFound',
      'This play session does not exist.',
    );
    await settle();
    http.expectOne({ method: 'POST', url: '/api/sessions' }).flush(gameState());
    await settle();

    expect(text('.banner.notice')).toContain('no longer had your previous session');
    expect(sessionStorage.getItem('tic-tac-toe.sessionId')).toBe('session-1');
    expect(text('[data-testid=status]')).toBe("Player X's turn");
  });

  it('switches mode through the API and renders the new game with the kept scoreboard', async () => {
    await startWith(gameState({ ...played(0, 4), scoreboard: { xWins: 2, oWins: 1, draws: 0 } }));

    button('Play against computer').click();
    await settle();
    const req = http.expectOne({ method: 'POST', url: '/api/sessions/session-1/games' });
    expect(req.request.body).toEqual({ mode: 'Computer' });
    req.flush(
      gameState({
        gameId: 'game-2',
        mode: 'Computer',
        scoreboard: { xWins: 2, oWins: 1, draws: 0 },
      }),
    );
    await settle();

    expect(button('Play against computer').getAttribute('aria-pressed')).toBe('true');
    expect(text('[data-testid=status]')).toBe('Your turn — you are X');
    expect(cells().every((c) => c.textContent?.trim() === '')).toBe(true);
    expect(text('[data-testid=x-wins]')).toBe('2');
  });

  it('undo, reset game, and reset scoreboard each call their endpoint and render the reply', async () => {
    await startWith(
      gameState({ ...played(0, 4), canUndo: true, scoreboard: { xWins: 1, oWins: 0, draws: 0 } }),
    );

    button('Undo last move').click();
    await settle();
    const undo = http.expectOne({
      method: 'POST',
      url: '/api/sessions/session-1/games/game-1/undo',
    });
    expect(undo.request.body).toEqual({ expectedRevision: 2 });
    undo.flush(
      gameState({
        ...played(0),
        revision: 3,
        currentPlayer: 'O',
        canUndo: true,
        scoreboard: { xWins: 1, oWins: 0, draws: 0 },
      }),
    );
    await settle();
    expect(cells()[4].textContent?.trim()).toBe('');

    button('Reset scoreboard').click();
    await settle();
    http
      .expectOne({ method: 'POST', url: '/api/sessions/session-1/scoreboard/reset' })
      .flush({ xWins: 0, oWins: 0, draws: 0 });
    await settle();
    expect(text('[data-testid=x-wins]')).toBe('0');
    expect(cells()[0].textContent?.trim()).toBe('X'); // the board is not touched by a scoreboard reset

    button('Reset game').click();
    await settle();
    http
      .expectOne({ method: 'POST', url: '/api/sessions/session-1/games/game-1/reset' })
      .flush(gameState({ gameId: 'game-2' }));
    await settle();
    expect(cells().every((c) => c.textContent?.trim() === '')).toBe(true);
    expect(text('app-move-history')).toContain('No moves yet.');
  });
});
