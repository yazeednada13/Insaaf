import type { BottomTabBarProps } from 'expo-router/tabs';
import { useEffect, useState, type ReactNode } from 'react';
import { Keyboard, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Circle, Line, Path, Rect } from 'react-native-svg';

import { colors, fonts } from '@/constants/theme';

const INACTIVE = '#888888';
const INACTIVE_LABEL = '#999999';

interface TabConfig {
  label: string;
  icon: (active: boolean) => ReactNode;
  prominent?: boolean;
}

// Figma BottomNav.tsx. Decision D-N1: only the tabs whose screens exist are shown; الرئيسية is added
// in Phase 8A.
const TABS: Record<string, TabConfig> = {
  restaurants: {
    label: 'استكشاف',
    icon: (active) => (
      <Svg fill="none" height={22} stroke={active ? colors.primary : INACTIVE} strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.8} viewBox="0 0 24 24" width={22}>
        <Circle cx={11} cy={11} r={8} />
        <Line x1={21} x2={16.65} y1={21} y2={16.65} />
      </Svg>
    ),
  },
  scan: {
    label: 'مسح',
    prominent: true,
    icon: () => (
      <Svg fill="none" height={24} stroke="white" strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.8} viewBox="0 0 24 24" width={24}>
        <Rect height={5} rx={1} width={5} x={3} y={3} />
        <Rect height={5} rx={1} width={5} x={16} y={3} />
        <Rect height={5} rx={1} width={5} x={3} y={16} />
        <Path d="M16 16h5v5" />
        <Path d="M16 19h2" />
        <Path d="M19 16v2" />
        <Path d="M3 11h5" />
        <Path d="M11 3v5" />
        <Path d="M11 11h2v2h-2z" />
      </Svg>
    ),
  },
  activity: {
    label: 'النشاط',
    icon: (active) => (
      <Svg fill={active ? colors.primary : 'none'} height={22} stroke={active ? colors.primary : INACTIVE} strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.8} viewBox="0 0 24 24" width={22}>
        <Path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9" />
        <Path d="M13.73 21a2 2 0 01-3.46 0" />
      </Svg>
    ),
  },
  profile: {
    label: 'ملفي',
    icon: (active) => (
      <Svg fill={active ? colors.primary : 'none'} height={22} stroke={active ? colors.primary : INACTIVE} strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.8} viewBox="0 0 24 24" width={22}>
        <Path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" />
        <Circle cx={12} cy={7} r={4} />
      </Svg>
    ),
  },
};

function useKeyboardVisible() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const show = Keyboard.addListener('keyboardDidShow', () => setVisible(true));
    const hide = Keyboard.addListener('keyboardDidHide', () => setVisible(false));
    return () => {
      show.remove();
      hide.remove();
    };
  }, []);

  return visible;
}

export function BottomNav({ state, navigation }: BottomTabBarProps) {
  const insets = useSafeAreaInsets();
  // Hidden while typing (e.g. the review text) so it does not sit on top of the keyboard.
  const keyboardVisible = useKeyboardVisible();

  if (keyboardVisible) {
    return null;
  }

  return (
    <View style={[styles.bar, { paddingBottom: Math.max(insets.bottom, 8) }]}>
      {state.routes.map((route, index) => {
        const tab = TABS[route.name];
        if (!tab) {
          return null;
        }

        const isActive = state.index === index;
        const onPress = () => {
          const event = navigation.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
          if (!isActive && !event.defaultPrevented) {
            navigation.navigate(route.name);
          }
        };

        return (
          <Pressable
            accessibilityLabel={tab.label}
            accessibilityRole="tab"
            accessibilityState={{ selected: isActive }}
            key={route.key}
            onPress={onPress}
            style={styles.item}
          >
            {tab.prominent ? (
              <View style={styles.prominentIcon}>{tab.icon(isActive)}</View>
            ) : (
              <View style={styles.icon}>{tab.icon(isActive)}</View>
            )}
            <Text
              style={[
                styles.label,
                { color: tab.prominent || isActive ? colors.primary : INACTIVE_LABEL },
              ]}
            >
              {tab.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  bar: {
    alignItems: 'flex-end',
    backgroundColor: colors.background,
    borderTopColor: colors.divider,
    borderTopWidth: 1,
    flexDirection: 'row',
  },
  icon: {
    alignItems: 'center',
    height: 28,
    justifyContent: 'center',
  },
  item: {
    alignItems: 'center',
    flex: 1,
    gap: 2,
    paddingVertical: 8,
  },
  label: {
    fontFamily: fonts.arabicMedium,
    fontSize: 10,
    lineHeight: 14,
  },
  prominentIcon: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 16,
    boxShadow: '0 4px 16px rgba(36,53,42,0.35)',
    height: 52,
    justifyContent: 'center',
    marginBottom: 4,
    marginTop: -20,
    width: 52,
  },
});
