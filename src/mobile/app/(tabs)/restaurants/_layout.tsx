import { Stack } from 'expo-router';

// Discovery stack inside the استكشاف tab; the session guard lives in app/(tabs)/_layout.tsx.
export default function RestaurantsLayout() {
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="search" />
      <Stack.Screen name="[id]" />
      <Stack.Screen name="review/[reviewId]" />
    </Stack>
  );
}
