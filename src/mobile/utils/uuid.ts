/**
 * A random UUID v4 for idempotency keys (Phase 9A `clientUploadId`). It only has to be unique per
 * user and visit token, not secret, so Math.random is enough and no native crypto module is needed.
 */
export function randomUuid(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (char) => {
    const random = (Math.random() * 16) | 0;
    const value = char === 'x' ? random : (random & 0x3) | 0x8;
    return value.toString(16);
  });
}
