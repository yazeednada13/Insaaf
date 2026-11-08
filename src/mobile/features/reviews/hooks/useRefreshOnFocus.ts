import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef } from 'react';
import { AppState } from 'react-native';

/** Photo screens refetch at least this often while open (Phase 9A plan section 10, D-14). */
export const OPEN_SCREEN_REFRESH_MS = 5 * 60_000;

interface RefreshableQuery {
  isStale: boolean;
  refetch: () => unknown;
}

/**
 * Refetches a screen's query when the user returns to it (only if its data is stale), and every
 * 5 minutes while the screen stays focused with the app in the foreground. Tab screens stay mounted,
 * so `refetchOnMount` alone never runs again. App resume is handled app-wide by `focusManager`.
 */
export function useRefreshOnFocus(query: RefreshableQuery) {
  const latest = useRef(query);
  useEffect(() => {
    latest.current = query;
  });
  const firstFocus = useRef(true);

  useFocusEffect(
    useCallback(() => {
      if (firstFocus.current) {
        firstFocus.current = false;
      } else if (latest.current.isStale) {
        void latest.current.refetch();
      }

      const timer = setInterval(() => {
        if (AppState.currentState === 'active') {
          void latest.current.refetch();
        }
      }, OPEN_SCREEN_REFRESH_MS);

      return () => clearInterval(timer);
    }, []),
  );
}
