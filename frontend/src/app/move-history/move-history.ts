import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { GameMode, MoveRecord } from '../api/game.models';

@Component({
  selector: 'app-move-history',
  templateUrl: './move-history.html',
  styleUrl: './move-history.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MoveHistory {
  readonly moves = input.required<readonly MoveRecord[]>();
  readonly mode = input.required<GameMode>();
}
