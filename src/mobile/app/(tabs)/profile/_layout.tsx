import { Stack } from 'expo-router';

// The ملفي tab. Reviews and other profiles have routes here too, so Back stays in this tab.
export default function ProfileLayout() {
  return <Stack screenOptions={{ headerShown: false }} />;
}
