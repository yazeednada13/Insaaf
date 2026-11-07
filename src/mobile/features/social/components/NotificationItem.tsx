import { Pressable, StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights, textAlignStart } from '@/constants/theme';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import type { NotificationDto } from '@/types/social';
import { formatRelativeTime } from '@/utils/relativeTime';

// Spec §32 wording; the server sends only the type and the actor.
function message(notification: NotificationDto, name: string): string {
  switch (notification.type) {
    case 'like':
      return `أعجب ${name} بتقييمك.`;
    case 'comment':
      return `علّق ${name} على تقييمك.`;
    default:
      return `بدأ ${name} بمتابعتك.`;
  }
}

// Figma ActivityScreen.tsx notification icons.
const ICONS: Record<NotificationDto['type'], string> = {
  like: '♥',
  comment: '💬',
  follow: '👤',
};

interface NotificationItemProps {
  notification: NotificationDto;
  onPress: (notification: NotificationDto) => void;
}

// Figma ActivityScreen.tsx notification card. The badge sits at the end corner, as Figma places it in RTL.
export function NotificationItem({ notification, onPress }: NotificationItemProps) {
  const name = notification.actor.fullName ?? notification.actor.userName ?? '';

  return (
    <Pressable
      accessibilityRole="button"
      onPress={() => onPress(notification)}
      style={[styles.card, !notification.isRead && styles.unread]}
    >
      <View>
        <UserAvatar name={name} size={44} />
        <View style={styles.badge}>
          <Text style={styles.badgeIcon}>{ICONS[notification.type]}</Text>
        </View>
      </View>
      <View style={styles.body}>
        <Text style={styles.message}>{message(notification, name)}</Text>
        <Text style={styles.time}>{formatRelativeTime(notification.createdAt)}</Text>
      </View>
      {notification.isRead ? null : <View style={styles.dot} />}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 10,
    bottom: -2,
    end: -2,
    height: 20,
    justifyContent: 'center',
    position: 'absolute',
    width: 20,
  },
  badgeIcon: {
    color: '#FFFFFF',
    fontSize: 10,
    lineHeight: 14,
  },
  body: {
    flex: 1,
    minWidth: 0,
  },
  card: {
    alignItems: 'flex-start',
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: 16,
    borderWidth: 1,
    flexDirection: 'row',
    gap: 12,
    padding: 16,
  },
  dot: {
    backgroundColor: colors.accent,
    borderRadius: 4,
    height: 8,
    marginTop: 8,
    width: 8,
  },
  message: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.snugSm,
    textAlign: textAlignStart,
  },
  time: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 4,
    textAlign: textAlignStart,
  },
  unread: {
    backgroundColor: 'rgba(36,53,42,0.04)',
  },
});
