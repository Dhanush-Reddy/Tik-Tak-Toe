import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { GameMode, Scoreboard } from '../api/game.models';

@Component({
  selector: 'app-scoreboard',
  templateUrl: './scoreboard.html',
  styleUrl: './scoreboard.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScoreboardPanel {
  readonly scoreboard = input.required<Scoreboard>();
  readonly mode = input.required<GameMode>();
  readonly resetDisabled = input(false);
  readonly resetRequested = output<void>();
}
