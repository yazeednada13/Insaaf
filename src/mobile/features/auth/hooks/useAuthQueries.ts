import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { LoginRequest, RegisterRequest, UserDto } from '@/types/auth';
import * as authApi from '@/services/auth/authApi';

export const authQueryKeys = {
  currentUser: ['auth', 'me'] as const,
};

export function useLoginMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: LoginRequest) => authApi.login(request),
    onSuccess: (auth) => {
      queryClient.setQueryData(authQueryKeys.currentUser, auth.user);
    },
  });
}

export function useRegisterMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: RegisterRequest) => authApi.register(request),
    onSuccess: (auth) => {
      queryClient.setQueryData(authQueryKeys.currentUser, auth.user);
    },
  });
}

export function useRefreshMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: authApi.refresh,
    onSuccess: (auth) => {
      queryClient.setQueryData(authQueryKeys.currentUser, auth.user);
    },
  });
}

export function useCurrentUserQuery(enabled: boolean) {
  return useQuery({
    queryKey: authQueryKeys.currentUser,
    queryFn: authApi.getCurrentUser,
    enabled,
    staleTime: 60_000,
  });
}

export function useSetCurrentUser() {
  const queryClient = useQueryClient();

  return (user: UserDto | null) => {
    if (user) {
      queryClient.setQueryData(authQueryKeys.currentUser, user);
      return;
    }

    queryClient.removeQueries({ queryKey: authQueryKeys.currentUser });
  };
}
