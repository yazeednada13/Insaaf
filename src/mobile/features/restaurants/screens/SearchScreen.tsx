import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

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

const SEARCH_DEBOUNCE_MS = 300;
// Matches the API validation limit for `search`.
const SEARCH_MAX_LENGTH = 100;
const SEARCH_PAGE_SIZE = 50;

// Figma SearchScreen.tsx — restaurants section only. The people section and the
// الكل / المطاعم / الأشخاص tabs are out of Phase 3 scope.
export function SearchScreen() {
  const insets = useSafeAreaInsets();
  const formatDistance = useRestaurantDistance();
  const [query, setQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedQuery(query.trim()), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timeout);
  }, [query]);

  const hasQuery = debouncedQuery.length > 0;
  const searchQuery = useRestaurantListQuery(
    { search: debouncedQuery, pageSize: SEARCH_PAGE_SIZE },
    hasQuery,
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

    if (searchQuery.isPending) {
      return <RestaurantListSkeleton count={3} />;
    }

    if (searchQuery.isError) {
      return (
        <RestaurantListErrorState
          message={searchQuery.error.message}
          onRetry={() => void searchQuery.refetch()}
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

  const results = hasQuery && searchQuery.isSuccess ? searchQuery.data.items : [];

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
      </View>

      <FlatList
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={renderEmpty()}
        ListHeaderComponent={
          results.length > 0 ? <Text style={styles.sectionTitle}>المطاعم</Text> : null
        }
        contentContainerStyle={styles.listContent}
        data={results}
        keyExtractor={(item) => item.id}
        keyboardShouldPersistTaps="handled"
        renderItem={({ item }) => (
          <RestaurantCard
            distance={formatDistance(item)}
            onPress={() => router.push({ pathname: '/restaurants/[id]', params: { id: item.id } })}
            restaurant={item}
          />
        )}
      />
    </View>
  );
}

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
