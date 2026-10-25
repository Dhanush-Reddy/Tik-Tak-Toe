import { GameState, Player } from '../api/game.models';

/** A GameState as the API would return it; tests override only what they care about. */
export function gameState(overrides: Partial<GameState> = {}): GameState {
  return {
    sessionId: 'session-1',
    gameId: 'game-1',
    revision: 0,
    mode: 'TwoPlayer',
    status: 'InProgress',
    currentPlayer: 'X',
    winner: null,
    winningCells: [],
    board: Array<Player | null>(9).fill(null),
    moves: [],
    canUndo: false,
    scoreboard: { xWins: 0, oWins: 0, draws: 0 },
    ...overrides,
  };
}

/** Builds board + history from cell indexes played alternately by X and O. */
export function played(...cells: number[]): Pick<GameState, 'board' | 'moves' | 'revision'> {
  const board = Array<Player | null>(9).fill(null);
  const moves = cells.map((cell, i) => {
    const player: Player = i % 2 === 0 ? 'X' : 'O';
    board[cell] = player;
    return { number: i + 1, player, row: Math.floor(cell / 3), column: cell % 3 };
  });
  return { board, moves, revision: cells.length };
}
