import { Stack } from 'expo-router';

// The النشاط tab. Review Detail has a route here too, so Back returns to the feed.
export default function ActivityLayout() {
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="review/[reviewId]" />
    </Stack>
  );
}
