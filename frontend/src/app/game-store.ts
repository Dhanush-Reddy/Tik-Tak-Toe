import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, toApiError } from './api/api-error';
import { GameApi } from './api/game-api';
import { GameMode, GameState } from './api/game.models';

export type PendingAction =
  'connect' | 'move' | 'undo' | 'reset' | 'mode' | 'scoreboard' | 'reload';

const SESSION_KEY = 'tic-tac-toe.sessionId';

/**
 * Holds the last game state confirmed by the server, plus request and error state for the UI.
 * It never applies game rules itself: every change comes from an API response. One request runs
 * at a time and actions started while one is pending are ignored. A failed mutation is never
 * replayed: the store reloads authoritative state and lets the person act again.
 */
@Injectable({ providedIn: 'root' })
export class GameStore {
  private readonly api = inject(GameApi);

  private readonly stateSignal = signal<GameState | null>(null);
  private readonly pendingSignal = signal<PendingAction | null>(null);
  private readonly errorSignal = signal<string | null>(null);
  private readonly noticeSignal = signal<string | null>(null);

  readonly state = this.stateSignal.asReadonly();
  readonly pending = this.pendingSignal.asReadonly();
  readonly error = this.errorSignal.asReadonly();
  readonly notice = this.noticeSignal.asReadonly();

  readonly busy = computed(() => this.pending() !== null);
  readonly canPlay = computed(() => this.state()?.status === 'InProgress' && !this.busy());
  readonly canUndo = computed(() => !!this.state()?.canUndo && !this.busy());
  readonly canReset = computed(() => this.state() !== null && !this.busy());

  /** Resumes this tab's session if the server still has it, otherwise starts a new one. */
  start(): Promise<void> {
    return this.run('connect', async () => {
      const sessionId = readStoredSessionId();
      if (sessionId) {
        try {
          return await firstValueFrom(this.api.getCurrentGame(sessionId));
        } catch (error) {
          if (toApiError(error).code !== 'SessionNotFound') {
            throw error;
          }
          this.noticeSignal.set(EXPIRED_SESSION_NOTICE);
        }
      }
      return firstValueFrom(this.api.createSession('TwoPlayer'));
    });
  }

  play(cell: number): Promise<void> {
    const state = this.state();
    const player = state?.currentPlayer;
    if (!state || !player || !this.canPlay() || state.board[cell] !== null) {
      return Promise.resolve();
    }

    const move = {
      player,
      row: Math.floor(cell / 3),
      column: cell % 3,
      expectedRevision: state.revision,
    };
    return this.run('move', () => firstValueFrom(this.api.submitMove(state, move)));
  }

  undo(): Promise<void> {
    const state = this.state();
    if (!state || !this.canUndo()) {
      return Promise.resolve();
    }
    return this.run('undo', () => firstValueFrom(this.api.undo(state, state.revision)));
  }

  resetGame(): Promise<void> {
    const state = this.state();
    if (!state || !this.canReset()) {
      return Promise.resolve();
    }
    return this.run('reset', () => firstValueFrom(this.api.resetGame(state)));
  }

  changeMode(mode: GameMode): Promise<void> {
    const state = this.state();
    if (!state || state.mode === mode || this.busy()) {
      return Promise.resolve();
    }
    return this.run('mode', () => firstValueFrom(this.api.startNewGame(state.sessionId, mode)));
  }

  resetScoreboard(): Promise<void> {
    const state = this.state();
    if (!state || this.busy()) {
      return Promise.resolve();
    }
    return this.run('scoreboard', async () => {
      const scoreboard = await firstValueFrom(this.api.resetScoreboard(state.sessionId));
      // Only the counters changed on the server; the game itself is untouched.
      return { ...state, scoreboard };
    });
  }

  /** Recovery action: fetch the session's current state, or start over if nothing is loaded yet. */
  reload(): Promise<void> {
    const state = this.state();
    if (!state) {
      return this.start();
    }
    return this.run('reload', () => firstValueFrom(this.api.getCurrentGame(state.sessionId)));
  }

  dismissNotice(): void {
    this.noticeSignal.set(null);
  }

  private async run(action: PendingAction, request: () => Promise<GameState>): Promise<void> {
    if (this.busy()) {
      return; // a second click while a request is in flight does nothing
    }

    this.pendingSignal.set(action);
    this.errorSignal.set(null);
    try {
      this.accept(await request());
    } catch (error) {
      await this.recover(toApiError(error));
    } finally {
      this.pendingSignal.set(null);
    }
  }

  private accept(state: GameState): void {
    this.stateSignal.set(state);
    storeSessionId(state.sessionId);
  }

  /** Keeps the last confirmed state on screen and, where that is safe, refreshes it from the server. */
  private async recover(error: ApiError): Promise<void> {
    const state = this.state();
    try {
      if (error.code === 'SessionNotFound') {
        this.noticeSignal.set(EXPIRED_SESSION_NOTICE);
        this.accept(await firstValueFrom(this.api.createSession(state?.mode ?? 'TwoPlayer')));
        return;
      }

      if (error.code === 'GameNotFound' && state) {
        this.noticeSignal.set(
          'That game had already been replaced, so the current game was loaded.',
        );
        this.accept(await firstValueFrom(this.api.getCurrentGame(state.sessionId)));
        return;
      }

      this.errorSignal.set(error.code === 'StaleRevision' ? STALE_MESSAGE : error.message);
      if (error.status === 409 && state) {
        // The request was rejected without changing anything; show what the server has now.
        this.accept(await firstValueFrom(this.api.getCurrentGame(state.sessionId)));
      }
    } catch (followUp) {
      this.errorSignal.set(toApiError(followUp).message);
    }
  }
}

const EXPIRED_SESSION_NOTICE =
  'The server no longer had your previous session (sessions live in memory, so a restart clears them). A new session with an empty scoreboard was started.';

const STALE_MESSAGE =
  'The game changed before your action reached the server, so nothing was applied. The latest state is shown; please try again.';

function readStoredSessionId(): string | null {
  try {
    return sessionStorage.getItem(SESSION_KEY);
  } catch {
    return null; // storage can be unavailable (for example in some privacy modes)
  }
}

function storeSessionId(sessionId: string): void {
  try {
    sessionStorage.setItem(SESSION_KEY, sessionId);
  } catch {
    // Without storage a page refresh starts a new session; everything else still works.
  }
}
