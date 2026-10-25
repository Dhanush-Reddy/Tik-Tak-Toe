import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { GameMode, GameState } from './api/game.models';
import { Board } from './board/board';
import { GameStore } from './game-store';
import { MoveHistory } from './move-history/move-history';
import { ScoreboardPanel } from './scoreboard/scoreboard';

@Component({
  selector: 'app-root',
  imports: [Board, ScoreboardPanel, MoveHistory],
  templateUrl: './app.html',
  styleUrl: './app.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App implements OnInit {
  protected readonly store = inject(GameStore);

  protected readonly modes: { value: GameMode; label: string }[] = [
    { value: 'TwoPlayer', label: 'Two players' },
    { value: 'Computer', label: 'Play against computer' },
  ];

  protected readonly statusMessage = computed(() =>
    describeStatus(this.store.state(), this.store.pending() === 'connect'),
  );

  ngOnInit(): void {
    void this.store.start();
  }
}

function describeStatus(state: GameState | null, connecting: boolean): string {
  if (!state) {
    return connecting ? 'Connecting to the game server…' : 'Not connected to the game server.';
  }

  const computer = state.mode === 'Computer';
  switch (state.status) {
    case 'InProgress':
      return computer ? 'Your turn — you are X' : `Player ${state.currentPlayer}'s turn`;
    case 'Won':
      if (computer) {
        return state.winner === 'X' ? 'You win! (X)' : 'The computer wins (O)';
      }
      return `Player ${state.winner} wins!`;
    case 'Draw':
      return "It's a draw — the board is full.";
  }
}
