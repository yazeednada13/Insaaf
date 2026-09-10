import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts, typography } from '@/constants/theme';

export function AuthDivider() {
  return (
    <View style={styles.container}>
      <View style={styles.line} />
      <Text style={styles.label}>أو</Text>
      <View style={styles.line} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
  },
  label: {
    color: colors.textDivider,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.divider,
    lineHeight: typography.lineHeightBody,
  },
  line: {
    backgroundColor: colors.border,
    flex: 1,
    height: 1,
  },
});
