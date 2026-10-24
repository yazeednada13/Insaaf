export type PriceLevel = 1 | 2 | 3 | 4;

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
}

export interface RestaurantListItemDto {
  id: string;
  name: string;
  city: string;
  address: string | null;
  latitude: number;
  longitude: number;
  priceLevel: PriceLevel;
  imageUrl: string | null;
  categories: CategoryDto[];
  averageRating: number | null;
  reviewCount: number;
}

export interface RestaurantListResultDto {
  items: RestaurantListItemDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RestaurantDetailDto {
  id: string;
  name: string;
  description: string;
  city: string;
  address: string | null;
  latitude: number;
  longitude: number;
  priceLevel: PriceLevel;
  phone: string | null;
  imageUrl: string | null;
  categories: CategoryDto[];
  averageRating: number | null;
  reviewCount: number;
}

export interface RestaurantSearchParams {
  search?: string;
  city?: string;
  minRating?: number;
  priceLevel?: PriceLevel;
  category?: string;
  page?: number;
  pageSize?: number;
}

export const PRICE_LEVEL_LABELS: Record<PriceLevel, string> = {
  1: '$',
  2: '$$',
  3: '$$$',
  4: '$$$$',
};

export function formatPriceLevel(level: PriceLevel): string {
  return PRICE_LEVEL_LABELS[level];
}

export function formatLocation(city: string, address?: string | null): string {
  return address ? `${address}، ${city}` : city;
}

export function formatCuisine(categories: CategoryDto[]): string {
  return categories.map((category) => category.name).join(' · ');
}
