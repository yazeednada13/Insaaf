import { router } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { openUser } from '@/features/navigation/stackRoutes';
import { UserRow } from '@/features/profile/components/UserRow';
import { useUserSearchQuery } from '@/features/profile/hooks/useUserQueries';
import { RestaurantCard } from '@/features/restaurants/components/RestaurantCard';
import { CloseIcon, SearchIcon } from '@/features/restaurants/components/RestaurantIcons';
import {
  RestaurantListEmptyState,
  RestaurantListErrorState,
  RestaurantListSkeleton,
} from '@/features/restaurants/components/RestaurantListStates';
import { useRestaurantDistance } from '@/features/restaurants/hooks/useRestaurantDistance';
import { useRestaurantListQuery } from '@/features/restaurants/hooks/useRestaurantQueries';
import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import type { RestaurantListItemDto } from '@/types/restaurant';
import type { UserSearchItemDto } from '@/types/social';

const SEARCH_DEBOUNCE_MS = 300;
// Matches the API validation limit for `search`.
const SEARCH_MAX_LENGTH = 100;
const SEARCH_PAGE_SIZE = 50;

type SearchTab = 'all' | 'restaurants' | 'people';

const TABS: { id: SearchTab; label: string }[] = [
  { id: 'all', label: 'الكل' },
  { id: 'restaurants', label: 'المطاعم' },
  { id: 'people', label: 'الأشخاص' },
];

// Matches the API's minimum for people search.
const PEOPLE_MIN_LENGTH = 2;

// Figma SearchScreen.tsx: restaurants and people, with الكل / المطاعم / الأشخاص tabs.
export function SearchScreen() {
  const insets = useSafeAreaInsets();
  const formatDistance = useRestaurantDistance();
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [tab, setTab] = useState<SearchTab>('all');

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedQuery(query.trim()), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timeout);
  }, [query]);

  const hasQuery = debouncedQuery.length > 0;
  const showRestaurants = tab !== 'people';
  const showPeople = tab !== 'restaurants';
  const searchQuery = useRestaurantListQuery(
    { search: debouncedQuery, pageSize: SEARCH_PAGE_SIZE },
    hasQuery && showRestaurants,
  );
  const peopleQuery = useUserSearchQuery(
    debouncedQuery,
    debouncedQuery.length >= PEOPLE_MIN_LENGTH && showPeople,
  );

  const goBack = () => {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace('/restaurants');
    }
  };

  const renderEmpty = () => {
    if (!hasQuery) {
      return <RestaurantListEmptyState hint="ابحث عن مطعم أو شخص" variant="idle" />;
    }

    const pending =
      (showRestaurants && searchQuery.isPending)
      || (showPeople && debouncedQuery.length >= PEOPLE_MIN_LENGTH && peopleQuery.isPending);
    if (pending) {
      return <RestaurantListSkeleton count={3} />;
    }

    const failed = showRestaurants && searchQuery.isError ? searchQuery : peopleQuery;
    if (failed.isError) {
      return (
        <RestaurantListErrorState
          message={failed.error.message}
          onRetry={() => void failed.refetch()}
        />
      );
    }

    return (
      <View style={styles.noResults}>
        <Text style={styles.noResultsTitle}>لا نتائج لـ &quot;{debouncedQuery}&quot;</Text>
        <Text style={styles.noResultsHint}>جرّب كلمة بحث مختلفة</Text>
      </View>
    );
  };

  const rows = useMemo<SearchRow[]>(() => {
    const restaurants = hasQuery && showRestaurants ? searchQuery.data?.items ?? [] : [];
    const people = hasQuery && showPeople ? peopleQuery.data?.items ?? [] : [];

    return [
      ...(restaurants.length > 0 ? [{ key: 'restaurants', kind: 'heading', title: 'المطاعم' } as const] : []),
      ...restaurants.map((item) => ({ key: `r-${item.id}`, kind: 'restaurant', item }) as const),
      ...(people.length > 0 ? [{ key: 'people', kind: 'heading', title: 'الأشخاص' } as const] : []),
      ...people.map((item) => ({ key: `p-${item.id}`, kind: 'person', item }) as const),
    ];
  }, [hasQuery, showRestaurants, showPeople, searchQuery.data, peopleQuery.data]);

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <View style={styles.inputWrapper}>
            <SearchIcon color={colors.textMuted} size={16} strokeWidth={2} />
            <TextInput
              autoFocus
              maxLength={SEARCH_MAX_LENGTH}
              onChangeText={setQuery}
              placeholder="ابحث عن مطعم أو شخص..."
              placeholderTextColor={colors.textPlaceholder}
              returnKeyType="search"
              style={styles.input}
              textAlign={textAlignStart}
              value={query}
            />
            {query ? (
              <Pressable accessibilityLabel="مسح" hitSlop={8} onPress={() => setQuery('')}>
                <CloseIcon color={colors.textPlaceholder} size={14} strokeWidth={2.5} />
              </Pressable>
            ) : null}
          </View>
          <Pressable accessibilityRole="button" onPress={goBack}>
            <Text style={styles.cancel}>إلغاء</Text>
          </Pressable>
        </View>

        <View style={styles.tabs}>
          {TABS.map((item) => (
            <Pressable
              accessibilityRole="tab"
              accessibilityState={{ selected: tab === item.id }}
              key={item.id}
              onPress={() => setTab(item.id)}
              style={[styles.tab, tab === item.id && styles.tabSelected]}
            >
              <Text style={[styles.tabLabel, tab === item.id && styles.tabLabelSelected]}>{item.label}</Text>
            </Pressable>
          ))}
        </View>
      </View>

      <FlatList
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={renderEmpty()}
        contentContainerStyle={styles.listContent}
        data={rows}
        keyExtractor={(row) => row.key}
        keyboardShouldPersistTaps="handled"
        renderItem={({ item: row }) => {
          if (row.kind === 'heading') {
            return <Text style={styles.sectionTitle}>{row.title}</Text>;
          }

          if (row.kind === 'person') {
            return (
              <UserRow
                fullName={row.item.fullName}
                onPress={() => openUser('restaurants', row.item.id)}
                subtitle={`${row.item.reviewCount} تقييم · ${row.item.followerCount} متابع`}
                userName={row.item.userName}
              />
            );
          }

          return (
            <RestaurantCard
              distance={formatDistance(row.item)}
              onPress={() => router.push({ pathname: '/restaurants/[id]', params: { id: row.item.id } })}
              restaurant={row.item}
            />
          );
        }}
      />
    </View>
  );
}

type SearchRow =
  | { key: string; kind: 'heading'; title: string }
  | { key: string; kind: 'restaurant'; item: RestaurantListItemDto }
  | { key: string; kind: 'person'; item: UserSearchItemDto };

const styles = StyleSheet.create({
  cancel: {
    color: colors.primary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  header: {
    backgroundColor: colors.background,
    borderBottomColor: colors.divider,
    borderBottomWidth: 1,
    paddingBottom: 12,
    paddingHorizontal: restaurantLayout.searchHorizontalPadding,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  headerRow: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
  },
  input: {
    color: colors.textPrimary,
    flex: 1,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    padding: 0,
  },
  inputWrapper: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.primary,
    borderRadius: restaurantLayout.controlRadius,
    borderWidth: restaurantLayout.inputBorderWidth,
    flex: 1,
    flexDirection: 'row',
    gap: 8,
    paddingHorizontal: 16,
    paddingVertical: 12,
  },
  tab: {
    borderRadius: 999,
    paddingHorizontal: 16,
    paddingVertical: 6,
  },
  tabLabel: {
    color: colors.textMuted,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  tabLabelSelected: {
    color: colors.primaryForeground,
  },
  tabSelected: {
    backgroundColor: colors.primary,
  },
  tabs: {
    flexDirection: 'row',
    gap: 4,
    marginTop: 12,
  },
  listContent: {
    flexGrow: 1,
    paddingHorizontal: restaurantLayout.searchHorizontalPadding,
    paddingVertical: 16,
  },
  noResults: {
    alignItems: 'center',
    paddingVertical: 48,
  },
  noResultsHint: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  noResultsTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 4,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  sectionTitle: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 12,
    // Figma `tracking-wide` (0.025em).
    letterSpacing: 0.3,
    lineHeight: lineHeights.xs,
    marginBottom: 8,
    textAlign: textAlignStart,
  },
  separator: {
    height: 8,
  },
});
