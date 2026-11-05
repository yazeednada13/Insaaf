/**
 * The single place that knows the invoice QR format; mirrors the backend `VisitPayload`.
 * Payload: `insaaf://visit/<token>`, where the token is 32 random bytes in base64url (43 chars).
 */
export const VISIT_PAYLOAD_PREFIX = 'insaaf://visit/';
export const VISIT_TOKEN_LENGTH = 43;

const TOKEN_PATTERN = /^[A-Za-z0-9_-]{43}$/;

/** Returns the raw token for an Insaaf invoice QR, or null for any other code. */
export function extractVisitToken(scanned: string | null | undefined): string | null {
  const value = scanned?.trim();
  if (!value?.startsWith(VISIT_PAYLOAD_PREFIX)) {
    return null;
  }

  const token = value.slice(VISIT_PAYLOAD_PREFIX.length);
  return TOKEN_PATTERN.test(token) ? token : null;
}
