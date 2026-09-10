import { ActivityIndicator, Pressable, StyleSheet, Text } from 'react-native';

import {
  authLayout,
  colors,
  fonts,
  radius,
  typography,
} from '@/constants/theme';

interface AuthPrimaryButtonProps {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  loading?: boolean;
}

export function AuthPrimaryButton({
  label,
  onPress,
  disabled = false,
  loading = false,
}: AuthPrimaryButtonProps) {
  const isDisabled = disabled || loading;

  return (
    <Pressable
      accessibilityRole="button"
      disabled={isDisabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.button,
        isDisabled && styles.buttonDisabled,
        pressed && !isDisabled && styles.buttonPressed,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={colors.primaryForeground} />
      ) : (
        <Text style={styles.label}>{label}</Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: radius.button,
    justifyContent: 'center',
    marginTop: authLayout.primaryButtonMarginTop,
    paddingVertical: authLayout.primaryButtonPaddingVertical,
    width: '100%',
  },
  buttonDisabled: {
    opacity: 0.55,
  },
  buttonPressed: {
    backgroundColor: '#1A2820',
  },
  label: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: typography.button,
    lineHeight: typography.lineHeightBody,
  },
});
