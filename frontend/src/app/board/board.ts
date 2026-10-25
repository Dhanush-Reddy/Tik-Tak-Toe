import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Player } from '../api/game.models';

interface CellView {
  index: number;
  value: Player | null;
  label: string;
  winning: boolean;
  playable: boolean;
}

/**
 * Renders the 3×3 board from server state. Cells are real buttons; unplayable ones use
 * aria-disabled (not disabled) so keyboard focus is not lost when a cell is taken.
 */
@Component({
  selector: 'app-board',
  templateUrl: './board.html',
  styleUrl: './board.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Board {
  readonly board = input.required<readonly (Player | null)[]>();
  readonly winningCells = input<readonly number[]>([]);
  /** False while the game is over or a request is pending. */
  readonly playable = input(false);
  readonly cellSelected = output<number>();

  protected readonly cells = computed<CellView[]>(() =>
    this.board().map((value, index) => {
      const winning = this.winningCells().includes(index);
      const row = Math.floor(index / 3) + 1;
      const column = (index % 3) + 1;
      return {
        index,
        value,
        winning,
        playable: this.playable() && value === null,
        label: `Row ${row}, column ${column}, ${value ?? 'empty'}${winning ? ', winning cell' : ''}`,
      };
    }),
  );

  protected select(cell: CellView): void {
    if (cell.playable) {
      this.cellSelected.emit(cell.index);
    }
  }
}
