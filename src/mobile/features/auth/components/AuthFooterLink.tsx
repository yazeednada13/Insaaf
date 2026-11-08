import { StyleSheet, Text, View } from 'react-native';

import { authLayout, colors, fonts, typography } from '@/constants/theme';

interface AuthFooterLinkProps {
  prompt: string;
  actionLabel: string;
  onPress: () => void;
}

export function AuthFooterLink({ prompt, actionLabel, onPress }: AuthFooterLinkProps) {
  return (
    <View style={styles.container}>
      {/* A nested Text (not a Pressable view) keeps the link on the same baseline as the prompt. */}
      <Text style={styles.text}>
        {prompt}{' '}
        <Text accessibilityRole="link" onPress={onPress} style={styles.action} suppressHighlighting>
          {actionLabel}
        </Text>
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
