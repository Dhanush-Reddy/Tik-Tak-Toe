import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { GameMode, GameState, MoveRequest, Scoreboard } from './game.models';

/** Typed wrapper around the REST API. Relative URLs go through the dev-server proxy (proxy.conf.json). */
@Injectable({ providedIn: 'root' })
export class GameApi {
  private readonly http = inject(HttpClient);
  private readonly sessions = '/api/sessions';

  createSession(mode: GameMode): Observable<GameState> {
    return this.http.post<GameState>(this.sessions, { mode });
  }

  getCurrentGame(sessionId: string): Observable<GameState> {
    return this.http.get<GameState>(`${this.sessions}/${sessionId}`);
  }

  startNewGame(sessionId: string, mode: GameMode): Observable<GameState> {
    return this.http.post<GameState>(`${this.sessions}/${sessionId}/games`, { mode });
  }

  submitMove(game: GameState, move: MoveRequest): Observable<GameState> {
    return this.http.post<GameState>(`${this.gameUrl(game)}/moves`, move);
  }

  undo(game: GameState, expectedRevision: number): Observable<GameState> {
    return this.http.post<GameState>(`${this.gameUrl(game)}/undo`, { expectedRevision });
  }

  resetGame(game: GameState): Observable<GameState> {
    return this.http.post<GameState>(`${this.gameUrl(game)}/reset`, null);
  }

  resetScoreboard(sessionId: string): Observable<Scoreboard> {
    return this.http.post<Scoreboard>(`${this.sessions}/${sessionId}/scoreboard/reset`, null);
  }

  private gameUrl(game: GameState): string {
    return `${this.sessions}/${game.sessionId}/games/${game.gameId}`;
  }
}
