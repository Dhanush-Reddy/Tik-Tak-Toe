// Mirrors backend/src/TicTacToe.Api/Contracts. Rows and columns are zero-based; `board` and
// `winningCells` use the row-major cell index (row * 3 + column).

export type Player = 'X' | 'O';
export type GameMode = 'TwoPlayer' | 'Computer';
export type GameStatus = 'InProgress' | 'Won' | 'Draw';

export interface MoveRecord {
  number: number;
  player: Player;
  row: number;
  column: number;
}

export interface Scoreboard {
  xWins: number;
  oWins: number;
  draws: number;
}

export interface GameState {
  sessionId: string;
  gameId: string;
  revision: number;
  mode: GameMode;
  status: GameStatus;
  currentPlayer: Player | null;
  winner: Player | null;
  winningCells: number[];
  board: (Player | null)[];
  moves: MoveRecord[];
  canUndo: boolean;
  scoreboard: Scoreboard;
}

export interface MoveRequest {
  player: Player;
  row: number;
  column: number;
  expectedRevision: number;
}

/** RFC 9457 problem details as returned by the API, plus its stable `code`. */
export interface ApiProblem {
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}
