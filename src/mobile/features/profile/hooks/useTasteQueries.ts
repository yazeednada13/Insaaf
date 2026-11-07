import { useQuery } from '@tanstack/react-query';

import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { TasteDto, TasteMatchDto } from '@/types/social';

export const tasteQueryKeys = {
  taste: (userId: string) => ['users', 'taste', userId] as const,
  match: (userId: string) => ['users', 'taste-match', userId] as const,
};

export function useTasteQuery(userId: string | undefined) {
  return useQuery<TasteDto, SocialApiError>({
    queryKey: tasteQueryKeys.taste(userId ?? ''),
    queryFn: () => socialApi.getTaste(userId!),
    enabled: Boolean(userId),
    staleTime: 5 * 60_000,
  });
}

export function useTasteMatchQuery(userId: string | undefined, enabled: boolean) {
  return useQuery<TasteMatchDto, SocialApiError>({
    queryKey: tasteQueryKeys.match(userId ?? ''),
    queryFn: () => socialApi.getTasteMatch(userId!),
    enabled: enabled && Boolean(userId),
    staleTime: 5 * 60_000,
  });
}
