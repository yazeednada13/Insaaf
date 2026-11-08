import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights } from '@/constants/theme';

/**
 * Shown while older data is on screen but the latest refresh failed (offline, server down). The app
 * doesn't treat that as synced, so removed photos may still show until a refresh succeeds (D-16; no
 * Figma design).
 */
export function RefreshFailedNotice({ visible }: { visible: boolean }) {
  if (!visible) {
    return null;
  }

  return (
    <View accessibilityLiveRegion="polite" style={styles.notice}>
      <Text style={styles.text}>تعذر تحديث المحتوى. اسحب للتحديث.</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  notice: {
    backgroundColor: 'rgba(183,121,69,0.12)',
    borderRadius: 12,
    marginBottom: 12,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  text: {
    color: colors.textBody,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    textAlign: 'center',
  },
});
