import { CameraView, useCameraPermissions, type BarcodeScanningResult } from 'expo-camera';
import { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Animated,
  Easing,
  Linking,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Rect } from 'react-native-svg';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';

const VIEWFINDER_SIZE = 256;
const SCANNER_BACKGROUND = '#202522';
const SCANNER_TEXT = '#FAF9F5';
const SCANNER_HINT = 'rgba(250,249,245,0.6)';

interface ScannerViewProps {
  /** Camera runs only while the scan tab is focused and the scanner view is shown. */
  cameraActive: boolean;
  /** True while a scanned token is being resolved: scans are ignored and a spinner is shown. */
  busy: boolean;
  onScanned: (data: string) => void;
}

// Figma QRScreen.tsx "scanner" view. The prototype's simulate-scan buttons are not built; the
// permission states are not in Figma (decision D-M3).
export function ScannerView({ cameraActive, busy, onScanned }: ScannerViewProps) {
  const insets = useSafeAreaInsets();
  const [permission, requestPermission] = useCameraPermissions();
  const granted = permission?.granted ?? false;

  const handleScanned = ({ data }: BarcodeScanningResult) => onScanned(data);

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Text style={styles.title}>امسح رمز الفاتورة</Text>
      </View>

      <View style={styles.body}>
        <Text style={styles.hint}>وجّه الكاميرا نحو رمز QR الموجود على فاتورتك</Text>

        <View style={styles.viewfinder}>
          {granted && cameraActive ? (
            <CameraView
              barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
              facing="back"
              onBarcodeScanned={busy ? undefined : handleScanned}
              style={StyleSheet.absoluteFill}
            />
          ) : (
            <View style={styles.placeholder}>
              <Svg fill="white" height={100} viewBox="0 0 100 100" width={100}>
                <Rect height={30} rx={2} width={30} x={10} y={10} />
                <Rect height={30} rx={2} width={30} x={60} y={10} />
                <Rect height={30} rx={2} width={30} x={10} y={60} />
                <Rect height={10} width={10} x={45} y={45} />
                <Rect height={10} width={10} x={60} y={60} />
                <Rect height={10} width={15} x={75} y={60} />
                <Rect height={15} width={10} x={60} y={75} />
              </Svg>
            </View>
          )}

          <View style={[styles.corner, styles.cornerTopStart]} />
          <View style={[styles.corner, styles.cornerTopEnd]} />
          <View style={[styles.corner, styles.cornerBottomStart]} />
          <View style={[styles.corner, styles.cornerBottomEnd]} />
          {granted && !busy ? <ScanLine /> : null}

          {busy ? (
            <View style={styles.busyOverlay}>
              <ActivityIndicator color={SCANNER_TEXT} size="large" />
            </View>
          ) : null}
        </View>

        {permission && !granted ? (
          <View style={styles.permission}>
            <Text style={styles.permissionText}>يحتاج إنصاف إلى الكاميرا لمسح رمز الفاتورة.</Text>
            <Pressable
              accessibilityRole="button"
              onPress={() => {
                if (permission.canAskAgain) {
                  void requestPermission();
                } else {
                  void Linking.openSettings();
                }
              }}
              style={styles.permissionButton}
            >
              <Text style={styles.permissionButtonLabel}>
                {permission.canAskAgain ? 'السماح باستخدام الكاميرا' : 'فتح الإعدادات'}
              </Text>
            </Pressable>
          </View>
        ) : null}
      </View>
    </View>
  );
}

// Figma `animate-scan`: an accent line sweeping down the viewfinder.
function ScanLine() {
  const [progress] = useState(() => new Animated.Value(0));

  useEffect(() => {
    const loop = Animated.loop(
      Animated.timing(progress, {
        duration: 2000,
        easing: Easing.inOut(Easing.ease),
        toValue: 1,
        useNativeDriver: true,
      }),
    );
    loop.start();
    return () => loop.stop();
  }, [progress]);

  const translateY = progress.interpolate({
    inputRange: [0, 1],
    outputRange: [16, VIEWFINDER_SIZE - 16],
  });

  return <Animated.View style={[styles.scanLine, { transform: [{ translateY }] }]} />;
}

const CORNER_INSET = 12;

const styles = StyleSheet.create({
  body: {
    alignItems: 'center',
    flex: 1,
    gap: 24,
    justifyContent: 'center',
    paddingHorizontal: 32,
  },
  busyOverlay: {
    alignItems: 'center',
    backgroundColor: 'rgba(32,37,34,0.6)',
    bottom: 0,
    justifyContent: 'center',
    left: 0,
    position: 'absolute',
    right: 0,
    top: 0,
  },
  corner: {
    borderColor: colors.accent,
    borderRadius: 2,
    height: 32,
    position: 'absolute',
    width: 32,
  },
  cornerBottomEnd: {
    borderBottomWidth: 2,
    borderEndWidth: 2,
    bottom: CORNER_INSET,
    end: CORNER_INSET,
  },
  cornerBottomStart: {
    borderBottomWidth: 2,
    borderStartWidth: 2,
    bottom: CORNER_INSET,
    start: CORNER_INSET,
  },
  cornerTopEnd: {
    borderEndWidth: 2,
    borderTopWidth: 2,
    end: CORNER_INSET,
    top: CORNER_INSET,
  },
  cornerTopStart: {
    borderStartWidth: 2,
    borderTopWidth: 2,
    start: CORNER_INSET,
    top: CORNER_INSET,
  },
  header: {
    paddingBottom: 16,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  hint: {
    color: SCANNER_HINT,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: 'center',
  },
  permission: {
    alignItems: 'center',
    gap: 12,
    width: '100%',
  },
  permissionButton: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 16,
    paddingVertical: 14,
    width: '100%',
  },
  permissionButtonLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  permissionText: {
    color: SCANNER_HINT,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: 'center',
  },
  placeholder: {
    alignItems: 'center',
    bottom: 0,
    justifyContent: 'center',
    left: 0,
    opacity: 0.1,
    position: 'absolute',
    right: 0,
    top: 0,
  },
  scanLine: {
    experimental_backgroundImage: `linear-gradient(90deg, transparent, ${colors.accent}, transparent)`,
    height: 2,
    left: 16,
    position: 'absolute',
    right: 16,
    top: 0,
  },
  screen: {
    backgroundColor: SCANNER_BACKGROUND,
    flex: 1,
  },
  title: {
    color: SCANNER_TEXT,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
    textAlign: textAlignStart,
  },
  viewfinder: {
    backgroundColor: 'rgba(255,255,255,0.05)',
    borderRadius: 24,
    height: VIEWFINDER_SIZE,
    overflow: 'hidden',
    width: VIEWFINDER_SIZE,
  },
});
