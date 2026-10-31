import { useEffect, useState, type ReactNode } from 'react';
import { Animated, Easing } from 'react-native';

// Figma index.css `.animate-fade-in`: opacity 0 → 1 and translateY 8 → 0 over 0.4s ease-out.
export function FadeInView({ children }: { children: ReactNode }) {
  const [progress] = useState(() => new Animated.Value(0));

  useEffect(() => {
    Animated.timing(progress, {
      duration: 400,
      easing: Easing.out(Easing.ease),
      toValue: 1,
      useNativeDriver: true,
    }).start();
  }, [progress]);

  const translateY = progress.interpolate({ inputRange: [0, 1], outputRange: [8, 0] });

  return (
    <Animated.View style={{ opacity: progress, transform: [{ translateY }] }}>
      {children}
    </Animated.View>
  );
}
