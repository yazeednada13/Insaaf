import { Pressable, StyleSheet, Text, View } from 'react-native';

import { authLayout, colors, fonts, typography } from '@/constants/theme';

interface AuthFooterLinkProps {
  prompt: string;
  actionLabel: string;
  onPress: () => void;
}

export function AuthFooterLink({ prompt, actionLabel, onPress }: AuthFooterLinkProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.text}>
        {prompt}{' '}
        <Pressable accessibilityRole="button" onPress={onPress}>
          <Text style={styles.action}>{actionLabel}</Text>
        </Pressable>
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  action: {
    color: colors.primary,
    fontFamily: fonts.arabicSemiBold,
  },
  container: {
    alignItems: 'center',
    paddingBottom: authLayout.footerPaddingBottom,
  },
  text: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.body,
    lineHeight: typography.lineHeightBody,
    textAlign: 'center',
  },
});
