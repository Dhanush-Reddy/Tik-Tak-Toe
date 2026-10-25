import { HttpErrorResponse } from '@angular/common/http';
import { ApiProblem } from './game.models';

export interface ApiError {
  /** HTTP status, or 0 when the server could not be reached. */
  status: number;
  /** The API's stable error code, such as "CellOccupied", when the response carried one. */
  code: string | null;
  message: string;
}

export const UNREACHABLE_MESSAGE =
  "Can't reach the game server. Check that the backend is running on http://localhost:5080, then choose Reload game.";

export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return {
      status: -1,
      code: null,
      message: 'Something went wrong in the page. Reload the game.',
    };
  }

  const problem = isProblem(error.error) ? error.error : null;

  // No response at all, or the dev-server proxy answering for a backend that is down.
  if (error.status === 0 || (error.status >= 500 && !problem?.code)) {
    return { status: error.status, code: null, message: UNREACHABLE_MESSAGE };
  }

  const firstValidationError = problem?.errors
    ? Object.values(problem.errors).flat()[0]
    : undefined;
  return {
    status: error.status,
    code: problem?.code ?? null,
    message:
      problem?.detail ??
      firstValidationError ??
      problem?.title ??
      `The server answered ${error.status}.`,
  };
}

function isProblem(body: unknown): body is ApiProblem {
  return typeof body === 'object' && body !== null && ('title' in body || 'code' in body);
}
