import type { PriceLevel } from '@/types/restaurant';

export const CATEGORY_CHIPS = [
  { slug: null, label: 'الكل' },
  { slug: 'burger', label: 'برجر' },
  { slug: 'pizza', label: 'بيتزا' },
  { slug: 'sushi', label: 'سوشي' },
  { slug: 'coffee', label: 'قهوة' },
  { slug: 'chicken', label: 'دجاج' },
  { slug: 'desserts', label: 'حلويات' },
  { slug: 'italian', label: 'إيطالي' },
  { slug: 'lebanese', label: 'لبناني' },
] as const;

export const MIN_RATING_OPTIONS = [3, 3.5, 4, 4.5] as const;

export const PRICE_OPTIONS: PriceLevel[] = [1, 2, 3, 4];
