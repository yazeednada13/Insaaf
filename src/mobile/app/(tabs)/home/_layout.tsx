import { Stack } from 'expo-router';

// The الرئيسية tab. Reviews and profiles have routes here too, so Back stays in this tab.
export default function HomeLayout() {
  return <Stack screenOptions={{ headerShown: false }} />;
}
