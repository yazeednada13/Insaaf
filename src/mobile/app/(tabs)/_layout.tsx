import { Redirect } from 'expo-router';
import { Tabs } from 'expo-router/tabs';
import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { colors } from '@/constants/theme';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { BottomNav } from '@/features/navigation/BottomNav';

// The main app behind the session: every tab calls JWT-protected APIs. The group does not change
// URLs (/restaurants, /restaurants/[id], /scan).
export default function TabsLayout() {
  const { isAuthenticated, isBootstrapping } = useAuth();

  if (isBootstrapping) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator color={colors.primary} size="large" />
      </View>
    );
  }

  if (!isAuthenticated) {
    return <Redirect href="/login" />;
  }

  return (
    <Tabs screenOptions={{ headerShown: false }} tabBar={(props) => <BottomNav {...props} />}>
      <Tabs.Screen name="restaurants" />
      <Tabs.Screen name="scan" />
    </Tabs>
  );
}

const styles = StyleSheet.create({
  centered: {
    alignItems: 'center',
    backgroundColor: colors.background,
    flex: 1,
    justifyContent: 'center',
  },
});
