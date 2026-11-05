import { isAxiosError } from 'axios';

import { apiClient } from '@/services/api/client';
import type { ApiResponse } from '@/services/api/types';
import type {
  CreateVerifiedReviewRequest,
  ResolvedVisitDto,
  ScanErrorKind,
  VerifiedReviewCreatedDto,
} from '@/types/verification';

export class VerificationApiError extends Error {
  readonly code: string;
  readonly kind: ScanErrorKind;

  constructor(code: string) {
    super(code);
    this.name = 'VerificationApiError';
    this.code = code;
    this.kind = toScanErrorKind(code);
  }
}

// Server codes → Figma error views. Everything else (network, rate limit, validation) is "failed".
function toScanErrorKind(code: string): ScanErrorKind {
  switch (code) {
    case 'token_expired':
      return 'expired';
    case 'token_used':
      return 'used';
    case 'token_invalid':
      return 'invalid';
    default:
      return 'failed';
  }
}

function toVerificationApiError(error: unknown): VerificationApiError {
  if (error instanceof VerificationApiError) {
    return error;
  }

  if (isAxiosError<ApiResponse<unknown>>(error)) {
    const code = error.response?.data?.errors?.[0]?.code;
    if (code) {
      return new VerificationApiError(code);
    }

    if (!error.response) {
      return new VerificationApiError('network_error');
    }
  }

  return new VerificationApiError('unknown');
}

function unwrap<T>(envelope: ApiResponse<T>): T {
  if (!envelope.success || !envelope.data) {
    throw new VerificationApiError(envelope.errors?.[0]?.code ?? 'unknown');
  }

  return envelope.data;
}

/** Preview only: returns the token's restaurant without redeeming the token. */
export async function resolveVisit(token: string): Promise<ResolvedVisitDto> {
  try {
    const response = await apiClient.post<ApiResponse<ResolvedVisitDto>>(
      '/api/v1/verification/visits/resolve',
      { token },
    );
    return unwrap(response.data);
  } catch (error) {
    throw toVerificationApiError(error);
  }
}

/** Redeems the token and creates the verified review in one server transaction. */
export async function createVerifiedReview(
  request: CreateVerifiedReviewRequest,
): Promise<VerifiedReviewCreatedDto> {
  try {
    const response = await apiClient.post<ApiResponse<VerifiedReviewCreatedDto>>(
      '/api/v1/verification/reviews',
      request,
    );
    return unwrap(response.data);
  } catch (error) {
    throw toVerificationApiError(error);
  }
}
