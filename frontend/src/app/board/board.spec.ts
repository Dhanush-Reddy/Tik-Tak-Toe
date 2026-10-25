import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Player } from '../api/game.models';
import { Board } from './board';

describe('Board', () => {
  let fixture: ComponentFixture<Board>;
  let selected: number[];

  async function render(
    board: (Player | null)[],
    playable: boolean,
    winningCells: number[] = [],
  ): Promise<HTMLButtonElement[]> {
    fixture = TestBed.createComponent(Board);
    fixture.componentRef.setInput('board', board);
    fixture.componentRef.setInput('playable', playable);
    fixture.componentRef.setInput('winningCells', winningCells);
    selected = [];
    fixture.componentInstance.cellSelected.subscribe((cell) => selected.push(cell));
    await fixture.whenStable();
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
  }

  it('labels every cell with its one-based position and contents', async () => {
    const cells = await render(['X', null, null, null, 'O', null, null, null, null], true);

    expect(cells.map((c) => c.getAttribute('aria-label'))).toEqual([
      'Row 1, column 1, X',
      'Row 1, column 2, empty',
      'Row 1, column 3, empty',
      'Row 2, column 1, empty',
      'Row 2, column 2, O',
      'Row 2, column 3, empty',
      'Row 3, column 1, empty',
      'Row 3, column 2, empty',
      'Row 3, column 3, empty',
    ]);
    expect(cells[0].classList.contains('x')).toBe(true);
    expect(cells[4].classList.contains('o')).toBe(true);
  });

  it('emits only for empty cells while the board is playable', async () => {
    const cells = await render(['X', null, null, null, null, null, null, null, null], true);

    cells[0].click(); // occupied
    cells[7].click(); // empty

    expect(selected).toEqual([7]);
    expect(cells[0].getAttribute('aria-disabled')).toBe('true');
    expect(cells[7].getAttribute('aria-disabled')).toBe('false');
  });

  it('emits nothing when the board is not playable', async () => {
    const cells = await render(Array(9).fill(null), false);

    cells.forEach((c) => c.click());

    expect(selected).toEqual([]);
  });

  it('marks the winning cells visibly and in their labels', async () => {
    const cells = await render(['X', 'O', 'O', null, 'X', null, null, null, 'X'], false, [0, 4, 8]);

    expect(cells.filter((c) => c.classList.contains('winning')).length).toBe(3);
    expect(cells[8].getAttribute('aria-label')).toBe('Row 3, column 3, X, winning cell');
    expect(cells[1].classList.contains('winning')).toBe(false);
  });
});
