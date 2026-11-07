import { router, usePathname, type Href } from 'expo-router';

export type TabStack = 'restaurants' | 'activity' | 'profile';

/**
 * The tab the current screen belongs to. Profile and Review Detail have a route in each tab's
 * stack, so Back returns to where the user came from.
 */
export function useCurrentStack(): TabStack {
  const pathname = usePathname();

  if (pathname.startsWith('/activity')) {
    return 'activity';
  }

  return pathname.startsWith('/profile') ? 'profile' : 'restaurants';
}

export function openReview(stack: TabStack, reviewId: string) {
  router.push(`/${stack}/review/${reviewId}` as Href);
}

export function openUser(stack: TabStack, userId: string) {
  router.push(`/${stack}/users/${userId}` as Href);
}

export function openUserList(stack: TabStack, userId: string, kind: 'followers' | 'following') {
  router.push(`/${stack}/users/${userId}/${kind}` as Href);
}
