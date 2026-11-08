import { isAxiosError } from 'axios';

import { apiClient } from '@/services/api/client';
import type { ApiResponse } from '@/services/api/types';
import type {
  CreateVerifiedReviewRequest,
  PhotoRemovedDto,
  ResolvedVisitDto,
  ScanErrorKind,
  UploadedPhotoDto,
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

/** A local, already compressed JPEG ready to upload. */
export interface PhotoFile {
  uri: string;
  name: string;
  type: 'image/jpeg';
}

/**
 * Phase 9A: uploads one photo for the visit token. Repeating it with the same clientUploadId returns
 * the same photo, so a retry after a lost response never creates a duplicate. Never redeems the token.
 */
export async function uploadPhoto(token: string, clientUploadId: string, file: PhotoFile): Promise<UploadedPhotoDto> {
  const form = new FormData();
  form.append('token', token);
  form.append('clientUploadId', clientUploadId);
  // React Native's FormData accepts a { uri, name, type } file descriptor.
  form.append('file', file as unknown as Blob);

  try {
    const response = await apiClient.post<ApiResponse<UploadedPhotoDto>>('/api/v1/verification/photos', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    });
    return unwrap(response.data);
  } catch (error) {
    throw toVerificationApiError(error);
  }
}

/** Phase 9A: removes the caller's own photo before it's attached, freeing its slot. */
export async function deletePhoto(photoId: string): Promise<PhotoRemovedDto> {
  try {
    const response = await apiClient.delete<ApiResponse<PhotoRemovedDto>>(`/api/v1/verification/photos/${photoId}`);
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
