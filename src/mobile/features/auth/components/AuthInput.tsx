import { useState } from 'react';
import {
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';

import {
  authLayout,
  colors,
  fonts,
  radius,
  typography,
} from '@/constants/theme';

interface AuthInputProps extends TextInputProps {
  label: string;
  error?: string;
}

export function AuthInput({ label, error, style, onFocus, onBlur, ...props }: AuthInputProps) {
  const [isFocused, setIsFocused] = useState(false);

  const borderColor = error ? colors.error : isFocused ? colors.primary : colors.border;

  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        placeholderTextColor={colors.textSecondary}
        style={[styles.input, { borderColor }, style]}
        textAlign="right"
        onBlur={(event) => {
          setIsFocused(false);
          onBlur?.(event);
        }}
        onFocus={(event) => {
          setIsFocused(true);
          onFocus?.(event);
        }}
        {...props}
      />
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: authLayout.labelInputGap,
  },
  error: {
    color: colors.error,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.label,
    lineHeight: typography.lineHeightBody,
    textAlign: 'right',
  },
  input: {
    backgroundColor: colors.surface,
    borderRadius: radius.input,
    borderWidth: authLayout.inputBorderWidth,
    color: colors.textPrimary,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.body,
    lineHeight: typography.lineHeightBody,
    paddingHorizontal: authLayout.inputPaddingHorizontal,
    paddingVertical: authLayout.inputPaddingVertical,
    textAlign: 'right',
    width: '100%',
  },
  label: {
    color: colors.textLabel,
    fontFamily: fonts.arabicMedium,
    fontSize: typography.label,
    lineHeight: typography.lineHeightBody,
    textAlign: 'right',
  },
});
