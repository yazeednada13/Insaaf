import { useEffect, useRef, useState } from 'react';
import { Animated, Easing } from 'react-native';
import Svg, { Line, Path, Polygon } from 'react-native-svg';

import { colors } from '@/constants/theme';

interface IconProps {
  size: number;
}

const HEART_PATH =
  'M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z';

// Founder decision: an SVG heart replaces Figma's ♡/♥ text glyphs. A thin outline when empty, solid
// copper when liked, with the subtle like animation from spec §47.
export function HeartIcon({ size, filled }: IconProps & { filled: boolean }) {
  const [scale] = useState(() => new Animated.Value(1));
  const wasFilled = useRef(filled);

  useEffect(() => {
    if (filled && !wasFilled.current) {
      Animated.sequence([
        Animated.timing(scale, {
          duration: 110,
          easing: Easing.out(Easing.quad),
          toValue: 1.25,
          useNativeDriver: true,
        }),
        Animated.timing(scale, {
          duration: 130,
          easing: Easing.inOut(Easing.quad),
          toValue: 1,
          useNativeDriver: true,
        }),
      ]).start();
    }
    wasFilled.current = filled;
  }, [filled, scale]);

  return (
    <Animated.View style={{ transform: [{ scale }] }}>
      <Svg
        fill={filled ? colors.accent : 'none'}
        height={size}
        stroke={filled ? colors.accent : '#888888'}
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth={1.8}
        viewBox="0 0 24 24"
        width={size}
      >
        <Path d={HEART_PATH} />
      </Svg>
    </Animated.View>
  );
}

// Figma ReviewCard.tsx / ReviewDetailScreen.tsx comment bubble.
export function CommentIcon({ size }: IconProps) {
  return (
    <Svg
      fill="none"
      height={size}
      stroke="#888888"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth={1.8}
      viewBox="0 0 24 24"
      width={size}
    >
      <Path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z" />
    </Svg>
  );
}

// Figma ReviewDetailScreen.tsx send button.
export function SendIcon({ size }: IconProps) {
  return (
    <Svg
      fill="none"
      height={size}
      stroke="white"
      strokeLinecap="round"
      strokeWidth={2.5}
      viewBox="0 0 24 24"
      width={size}
    >
      <Line x1={22} x2={11} y1={2} y2={13} />
      <Polygon points="22 2 15 22 11 13 2 9 22 2" />
    </Svg>
  );
}
