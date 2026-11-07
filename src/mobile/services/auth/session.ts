import type { UserDto } from '@/types/auth';

interface SessionState {
  accessToken: string | null;
  accessTokenExpiresAt: string | null;
  user: UserDto | null;
}

const clearedListeners = new Set<() => void>();

const session: SessionState = {
  accessToken: null,
  accessTokenExpiresAt: null,
  user: null,
};

export function getAccessToken(): string | null {
  return session.accessToken;
}

export function getSessionUser(): UserDto | null {
  return session.user;
}

export function isAccessTokenExpired(): boolean {
  if (!session.accessTokenExpiresAt) {
    return true;
  }

  return new Date(session.accessTokenExpiresAt).getTime() <= Date.now();
}

export function setSession(
  accessToken: string,
  accessTokenExpiresAt: string,
  user: UserDto,
): void {
  session.accessToken = accessToken;
  session.accessTokenExpiresAt = accessTokenExpiresAt;
  session.user = user;
}

export function clearSession(): void {
  session.accessToken = null;
  session.accessTokenExpiresAt = null;
  session.user = null;
  clearedListeners.forEach((listener) => listener());
}

/** Tells the app the session ended, including forced sign-out from the Axios interceptor. */
export function onSessionCleared(listener: () => void): () => void {
  clearedListeners.add(listener);
  return () => {
    clearedListeners.delete(listener);
  };
}
