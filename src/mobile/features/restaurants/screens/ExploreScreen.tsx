import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { FilterChip } from '@/features/restaurants/components/FilterChip';
import { RestaurantCard } from '@/features/restaurants/components/RestaurantCard';
import { RestaurantFilterSheet } from '@/features/restaurants/components/RestaurantFilterSheet';
import { FilterIcon, SearchIcon } from '@/features/restaurants/components/RestaurantIcons';
import {
  RestaurantListEmptyState,
  RestaurantListErrorState,
  RestaurantListSkeleton,
} from '@/features/restaurants/components/RestaurantListStates';
import { CATEGORY_CHIPS } from '@/features/restaurants/constants';
import { useRestaurantDistance } from '@/features/restaurants/hooks/useRestaurantDistance';
import { useRestaurantListQuery } from '@/features/restaurants/hooks/useRestaurantQueries';
import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import type { PriceLevel, RestaurantSearchParams } from '@/types/restaurant';

// The API caps pageSize at 50; the Amman pilot has 11 active restaurants, so one page covers discovery.
const EXPLORE_PAGE_SIZE = 50;

// Figma ExploreScreen.tsx.
export function ExploreScreen() {
  const insets = useSafeAreaInsets();
  const formatDistance = useRestaurantDistance();
  const [categorySlug, setCategorySlug] = useState<string | null>(null);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [minRating, setMinRating] = useState<number | null>(null);
  const [priceLevels, setPriceLevels] = useState<PriceLevel[]>([]);
  const [openNow, setOpenNow] = useState(false);

  // The API accepts one priceLevel per request (contract §6.5): a single selection is
  // filtered server-side, multiple selections are OR-filtered on the client.
  const filters: RestaurantSearchParams = {
    category: categorySlug ?? undefined,
    minRating: minRating ?? undefined,
    priceLevel: priceLevels.length === 1 ? priceLevels[0] : undefined,
    openNow: openNow || undefined,
    pageSize: EXPLORE_PAGE_SIZE,
  };

  const listQuery = useRestaurantListQuery(filters);

  const restaurants = useMemo(() => {
    const items = listQuery.data?.items ?? [];
    return priceLevels.length > 1
      ? items.filter((item) => priceLevels.includes(item.priceLevel))
      : items;
  }, [listQuery.data, priceLevels]);

  const resultCount = priceLevels.length > 1 ? restaurants.length : listQuery.data?.totalCount ?? 0;
  const activeCategory = CATEGORY_CHIPS.find((chip) => chip.slug === categorySlug);

  const togglePriceLevel = (level: PriceLevel) =>
    setPriceLevels((current) =>
      current.includes(level) ? current.filter((value) => value !== level) : [...current, level],
    );

  const resetFilters = () => {
    setMinRating(null);
    setPriceLevels([]);
    setOpenNow(false);
  };

  const renderEmpty = () => {
    if (listQuery.isPending) {
      return <RestaurantListSkeleton />;
    }

    if (listQuery.isError) {
      return (
        <RestaurantListErrorState
          message={listQuery.error.message}
          onRetry={() => void listQuery.refetch()}
        />
      );
    }

    return <RestaurantListEmptyState hint="جرّب تغيير الفلاتر أو الفئة" title="لا توجد نتائج" />;
  };

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Text style={styles.title}>استكشاف</Text>

        <Pressable
          accessibilityRole="search"
          onPress={() => router.push('/restaurants/search')}
          style={styles.searchBar}
        >
          <SearchIcon color={colors.textPlaceholder} size={16} strokeWidth={2} />
          <Text style={styles.searchPlaceholder}>ابحث عن مطعم أو شخص...</Text>
        </Pressable>

        <ScrollView
          contentContainerStyle={styles.chipRow}
          horizontal
          showsHorizontalScrollIndicator={false}
          style={styles.chipScroll}
        >
          {CATEGORY_CHIPS.map((chip) => (
            <FilterChip
              key={chip.label}
              label={chip.label}
              onPress={() => setCategorySlug(chip.slug)}
              selected={chip.slug === categorySlug}
            />
          ))}
        </ScrollView>
      </View>

      <FlatList
        ItemSeparatorComponent={() => <View style={styles.separator} />}
        ListEmptyComponent={renderEmpty()}
        ListHeaderComponent={
          <View style={styles.sectionHeader}>
            {/* Founder decision: the "all" title (الأعلى تقييمًا بالقرب منك) is hidden in Phase 3. */}
            {activeCategory?.slug ? (
              <Text style={styles.sectionTitle}>
                {activeCategory.label}
                {listQuery.isSuccess ? <Text style={styles.sectionCount}>  ({resultCount})</Text> : null}
              </Text>
            ) : (
              <View />
            )}
            <Pressable
              accessibilityRole="button"
              onPress={() => setFiltersOpen(true)}
              style={styles.filterButton}
            >
              <FilterIcon color={colors.textLabel} size={14} strokeWidth={2} />
              <Text style={styles.filterLabel}>فلترة</Text>
            </Pressable>
          </View>
        }
        contentContainerStyle={styles.listContent}
        data={listQuery.isSuccess ? restaurants : []}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <RestaurantCard
            distance={formatDistance(item)}
            onPress={() => router.push({ pathname: '/restaurants/[id]', params: { id: item.id } })}
            restaurant={item}
          />
        )}
      />

      <RestaurantFilterSheet
        minRating={minRating}
        onClose={() => setFiltersOpen(false)}
        onMinRatingChange={setMinRating}
        onReset={resetFilters}
        onToggleOpenNow={() => setOpenNow((current) => !current)}
        onTogglePriceLevel={togglePriceLevel}
        openNow={openNow}
        priceLevels={priceLevels}
        visible={filtersOpen}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  chipRow: {
    gap: 8,
    paddingBottom: 4,
    paddingHorizontal: restaurantLayout.horizontalPadding,
  },
  chipScroll: {
    marginHorizontal: -restaurantLayout.horizontalPadding,
  },
  filterButton: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: restaurantLayout.controlRadius,
    borderWidth: restaurantLayout.inputBorderWidth,
    flexDirection: 'row',
    gap: 6,
    paddingHorizontal: 12,
    paddingVertical: 6,
  },
  filterLabel: {
    color: colors.textLabel,
    fontFamily: fonts.arabicMedium,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  header: {
    paddingBottom: 12,
    paddingHorizontal: restaurantLayout.horizontalPadding,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  listContent: {
    flexGrow: 1,
    paddingBottom: 24,
    paddingHorizontal: restaurantLayout.horizontalPadding,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  searchBar: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: restaurantLayout.controlRadius,
    borderWidth: restaurantLayout.inputBorderWidth,
    flexDirection: 'row',
    gap: 12,
    marginBottom: 16,
    paddingHorizontal: 16,
    paddingVertical: 12,
  },
  searchPlaceholder: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  sectionCount: {
    color: colors.textPlaceholder,
    fontFamily: fonts.numericRegular,
  },
  sectionHeader: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  sectionTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  separator: {
    height: 8,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 24,
    lineHeight: lineHeights.xl2,
    marginBottom: 16,
    textAlign: textAlignStart,
  },
});
