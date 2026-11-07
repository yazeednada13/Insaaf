import { router, type Href } from 'expo-router';
import { useState } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import { AuthInput } from '@/features/auth/components/AuthInput';
import { useMyProfileQuery, useUpdateProfileMutation } from '@/features/profile/hooks/useUserQueries';
import { RestaurantListErrorState } from '@/features/restaurants/components/RestaurantListStates';
import { UserAvatar } from '@/features/reviews/components/UserAvatar';
import type { UserProfileDto } from '@/types/social';

/** Matches the backend limit (decision D-3). */
const BIO_MAX_LENGTH = 150;

function goBack() {
  if (router.canGoBack()) {
    router.back();
  } else {
    router.replace('/profile' as Href);
  }
}

// Figma EditProfileScreen.tsx. "تغيير الصورة" is hidden: avatars are out of scope (decision D-5).
function EditForm({ profile }: { profile: UserProfileDto }) {
  const insets = useSafeAreaInsets();
  const updateMutation = useUpdateProfileMutation();
  const [fullName, setFullName] = useState(profile.fullName ?? '');
  const [userName, setUserName] = useState(profile.userName ?? '');
  const [bio, setBio] = useState(profile.bio ?? '');

  const canSave = fullName.trim().length > 0 && userName.trim().length > 0 && !updateMutation.isPending;
  const error = updateMutation.error;
  const usernameError = error?.code === 'username_taken' ? error.message : undefined;

  const save = () => {
    updateMutation.mutate(
      { fullName: fullName.trim(), userName: userName.trim(), bio: bio.trim() || null },
      { onSuccess: goBack },
    );
  };

  return (
    <KeyboardAvoidingView behavior="padding" style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Pressable accessibilityRole="button" onPress={goBack}>
          <Text style={styles.cancel}>إلغاء</Text>
        </Pressable>
        <Text style={styles.title}>تعديل الملف</Text>
        <Pressable accessibilityRole="button" disabled={!canSave} onPress={save}>
          {updateMutation.isPending ? (
            <ActivityIndicator color={colors.primary} size="small" />
          ) : (
            <Text style={[styles.save, !canSave && styles.saveDisabled]}>حفظ</Text>
          )}
        </Pressable>
      </View>

      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <View style={styles.avatar}>
          <UserAvatar name={fullName || userName} size={90} />
        </View>

        <View style={styles.fields}>
          <AuthInput label="الاسم" onChangeText={setFullName} placeholder="اسمك الكامل" value={fullName} />
          <AuthInput
            autoCapitalize="none"
            autoCorrect={false}
            error={usernameError}
            label="اسم المستخدم"
            onChangeText={setUserName}
            placeholder="username"
            value={userName}
          />
          <View>
            <Text style={styles.label}>النبذة الشخصية</Text>
            <TextInput
              maxLength={BIO_MAX_LENGTH}
              multiline
              numberOfLines={3}
              onChangeText={setBio}
              style={styles.bio}
              textAlign={textAlignStart}
              textAlignVertical="top"
              value={bio}
            />
            <Text style={styles.counter}>
              {bio.length}/{BIO_MAX_LENGTH}
            </Text>
          </View>
          {error && !usernameError ? <Text style={styles.error}>{error.message}</Text> : null}
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

export function EditProfileScreen() {
  const profileQuery = useMyProfileQuery();

  if (profileQuery.isPending) {
    return (
      <View style={[styles.screen, styles.centered]}>
        <ActivityIndicator color={colors.primary} size="large" />
      </View>
    );
  }

  if (profileQuery.isError) {
    return (
      <View style={[styles.screen, styles.centered]}>
        <RestaurantListErrorState
          message={profileQuery.error.message}
          onRetry={() => void profileQuery.refetch()}
        />
      </View>
    );
  }

  return <EditForm profile={profileQuery.data} />;
}

const styles = StyleSheet.create({
  avatar: {
    alignItems: 'center',
    marginBottom: 24,
  },
  bio: {
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: 12,
    borderWidth: 1.5,
    color: colors.textPrimary,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    minHeight: 88,
    paddingHorizontal: 16,
    paddingVertical: 14,
  },
  cancel: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  centered: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  content: {
    paddingHorizontal: 20,
    paddingVertical: 24,
  },
  counter: {
    color: colors.textPlaceholder,
    fontFamily: fonts.numericRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 4,
    textAlign: 'right',
  },
  error: {
    color: colors.error,
    fontFamily: fonts.arabicRegular,
    fontSize: 13,
    lineHeight: lineHeights.sm,
    textAlign: 'center',
  },
  fields: {
    gap: 16,
  },
  header: {
    alignItems: 'center',
    borderBottomColor: colors.divider,
    borderBottomWidth: 1,
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingBottom: 12,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  label: {
    color: colors.textLabel,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 6,
    textAlign: textAlignStart,
  },
  save: {
    color: colors.primary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  saveDisabled: {
    opacity: 0.4,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
  },
});
