import { useQueryClient } from '@tanstack/react-query';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';

import type { LoginRequest, RegisterRequest, UserDto } from '@/types/auth';
import { applyAuthResponse, logoutSession } from '@/services/api/client';
import {
  useLoginMutation,
  useRefreshMutation,
  useRegisterMutation,
  useSetCurrentUser,
} from '@/features/auth/hooks/useAuthQueries';
import * as authApi from '@/services/auth/authApi';
import { getSessionUser, onSessionCleared } from '@/services/auth/session';
import { getRefreshToken } from '@/services/auth/tokenStorage';

interface AuthContextValue {
  user: UserDto | null;
  isAuthenticated: boolean;
  isBootstrapping: boolean;
  login: (request: LoginRequest) => Promise<void>;
  register: (request: RegisterRequest) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(getSessionUser());
  const [isBootstrapping, setIsBootstrapping] = useState(true);
  const loginMutation = useLoginMutation();
  const registerMutation = useRegisterMutation();
  const refreshMutation = useRefreshMutation();
  const setCurrentUser = useSetCurrentUser();
  const queryClient = useQueryClient();
  const accountId = useRef(user?.id ?? null);

  const syncUser = useCallback(
    (nextUser: UserDto | null) => {
      const nextAccountId = nextUser?.id ?? null;

      // Account isolation: whenever the account changes, by any path, nothing cached survives.
      if (nextAccountId !== accountId.current) {
        accountId.current = nextAccountId;
        void queryClient.cancelQueries();
        queryClient.clear();
      }

      setUser(nextUser);
      setCurrentUser(nextUser);
    },
    [queryClient, setCurrentUser],
  );

  // The Axios interceptor ends the session when a refresh fails, without calling logout().
  useEffect(() => onSessionCleared(() => syncUser(null)), [syncUser]);

  const bootstrap = useCallback(async () => {
    try {
      const refreshToken = await getRefreshToken();
      if (!refreshToken) {
        return;
      }

      const auth = await refreshMutation.mutateAsync({ refreshToken });
      await applyAuthResponse(auth);
      syncUser(auth.user);
    } catch {
      await logoutSession();
      syncUser(null);
    } finally {
      setIsBootstrapping(false);
    }
  }, [refreshMutation, syncUser]);

  // The stored refresh token is single-use (rotation + reuse detection), so startup
  // refresh must run exactly once; a second call with the same token revokes the session.
  const hasBootstrapped = useRef(false);

  useEffect(() => {
    if (hasBootstrapped.current) {
      return;
    }

    hasBootstrapped.current = true;
    void bootstrap();
  }, [bootstrap]);

  const login = useCallback(
    async (request: LoginRequest) => {
      const auth = await loginMutation.mutateAsync(request);
      await applyAuthResponse(auth);
      syncUser(auth.user);
    },
    [loginMutation, syncUser],
  );

  const register = useCallback(
    async (request: RegisterRequest) => {
      const auth = await registerMutation.mutateAsync(request);
      await applyAuthResponse(auth);
      syncUser(auth.user);
    },
    [registerMutation, syncUser],
  );

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {
      // The server is unreachable: sign-out still completes on this device.
    }

    await logoutSession();
    syncUser(null);
  }, [syncUser]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isBootstrapping,
      login,
      register,
      logout,
    }),
    [user, isBootstrapping, login, register, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuthContext(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuthContext must be used within AuthProvider');
  }

  return context;
}
