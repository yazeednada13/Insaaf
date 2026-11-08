import { router, useFocusEffect, type Href } from 'expo-router';
import { useCallback, useRef, useState } from 'react';
import { Alert } from 'react-native';

import { ScannerView } from '@/features/verification/components/ScannerView';
import { ScanErrorState, ScanSuccessState } from '@/features/verification/components/ScanStatusViews';
import { VerifiedRestaurantResult } from '@/features/verification/components/VerifiedRestaurantResult';
import {
  EMPTY_RATINGS,
  VerifiedReviewForm,
  type ReviewRatings,
} from '@/features/verification/components/VerifiedReviewForm';
import {
  useCreateVerifiedReviewMutation,
  useResolveVisitMutation,
} from '@/features/verification/hooks/useVerificationMutations';
import { useReviewPhotos, type PhotoItem } from '@/features/verification/hooks/useReviewPhotos';
import type { VerificationApiError } from '@/services/verification/verificationApi';
import type { ScanErrorKind, VerifiedRestaurantDto } from '@/types/verification';
import { extractVisitToken } from '@/utils/visitPayload';

type ScanView = 'scanner' | 'result' | 'write' | 'success' | 'error';

// Our copy for submit failures that keep the form: the token was not redeemed, so a retry is safe.
function submitErrorMessage(error: VerificationApiError): string {
  switch (error.code) {
    case 'invalid_photo':
      return 'تعذّر إرفاق بعض الصور. أعد رفعها أو أرسل بدونها.';
    case 'token_claimed':
      return 'هذا الرمز مستخدم من حساب آخر.';
    case 'try_again':
      return 'حاول مرة أخرى بعد قليل.';
  }

  if (error.code === 'rate_limited') {
    return 'محاولات كثيرة. انتظر قليلًا ثم حاول مرة أخرى.';
  }

  if (error.code === 'network_error') {
    return 'تعذّر الاتصال بالخادم. تحقق من اتصالك بالإنترنت.';
  }

  return 'تعذّر إرسال التقييم. حاول مرة أخرى.';
}

// Phase 9A: one line for failed photo uploads. The thumbnail itself offers retry and remove.
function photoErrorMessage(items: PhotoItem[]): string | null {
  const failed = items.find((item) => item.status === 'failed');
  switch (failed?.errorCode) {
    case undefined:
      return null;
    case 'file_too_large':
      return 'الصورة أكبر من 5 ميغابايت.';
    case 'unsupported_media_type':
    case 'compress_failed':
      return 'هذا الملف ليس صورة مدعومة. أزله وجرّب صورة أخرى.';
    case 'photo_limit':
      return 'يمكن إرفاق 5 صور كحد أقصى.';
    case 'token_claimed':
      return 'هذا الرمز مستخدم من حساب آخر.';
    case 'invalid_photo':
      return 'تعذّر إرفاق بعض الصور. أعد رفعها أو أرسل بدونها.';
    case 'rate_limited':
      return 'محاولات كثيرة. انتظر قليلًا ثم أعد المحاولة.';
    default:
      return 'تعذّر رفع بعض الصور. أعد المحاولة أو أزلها.';
  }
}

/**
 * Figma QRScreen.tsx as one screen with internal views (approved plan §6). The raw token lives
 * only in this screen's state: never in the URL or storage. The restaurant comes from the server.
 */
export function ScanScreen() {
  const [view, setView] = useState<ScanView>('scanner');
  const [errorKind, setErrorKind] = useState<ScanErrorKind>('invalid');
  const [token, setToken] = useState<string | null>(null);
  const [restaurant, setRestaurant] = useState<VerifiedRestaurantDto | null>(null);
  const [ratings, setRatings] = useState<ReviewRatings>(EMPTY_RATINGS);
  const [text, setText] = useState('');
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [focused, setFocused] = useState(false);
  // Camera callbacks fire repeatedly for one QR; only the first read is handled.
  const scanLocked = useRef(false);

  const resolveMutation = useResolveVisitMutation();
  const createMutation = useCreateVerifiedReviewMutation();
  const photos = useReviewPhotos(token);

  useFocusEffect(
    useCallback(() => {
      setFocused(true);
      return () => setFocused(false);
    }, []),
  );

  const showError = (kind: ScanErrorKind) => {
    setErrorKind(kind);
    setView('error');
  };

  const reset = () => {
    scanLocked.current = false;
    resolveMutation.reset();
    createMutation.reset();
    setToken(null);
    setRestaurant(null);
    setRatings(EMPTY_RATINGS);
    setText('');
    setSubmitError(null);
    photos.reset();
    setView('scanner');
  };

  const handleScanned = (data: string) => {
    if (scanLocked.current) {
      return;
    }
    scanLocked.current = true;

    const scannedToken = extractVisitToken(data);
    if (!scannedToken) {
      // Not an Insaaf invoice code: rejected locally, the server is not called.
      showError('invalid');
      return;
    }

    resolveMutation.mutate(scannedToken, {
      onSuccess: ({ restaurant: resolved }) => {
        setToken(scannedToken);
        setRestaurant(resolved);
        setView('result');
      },
      onError: (error) => showError(error.kind),
    });
  };

  const submit = () => {
    if (!token || createMutation.isPending) {
      return;
    }

    // Only uploaded photos are sent, in thumbnail order. Failed ones are left out (decision D-12).
    const photoIds = photos.uploadedIds;
    setSubmitError(null);
    createMutation.mutate(
      {
        token,
        rating: ratings.overall,
        foodRating: ratings.food,
        serviceRating: ratings.service,
        valueRating: ratings.value,
        atmosphereRating: ratings.atmosphere,
        text: text.trim() || null,
        photoIds,
      },
      {
        onSuccess: () => setView('success'),
        onError: (error) => {
          if (error.code === 'invalid_photo') {
            // The token was not used: the photos must be re-uploaded or left out (plan section 9).
            photos.markFailed(photoIds);
          }

          if (error.kind === 'failed') {
            setSubmitError(submitErrorMessage(error));
          } else {
            showError(error.kind);
          }
        },
      },
    );
  };

  const handleSubmit = () => {
    if (photos.isBusy) {
      return;
    }

    if (photos.failedCount > 0) {
      Alert.alert('', 'بعض الصور لم تُرفع. إرسال التقييم بدونها؟', [
        { style: 'cancel', text: 'رجوع' },
        { onPress: submit, text: 'إرسال' },
      ]);
      return;
    }

    submit();
  };

  if (view === 'error') {
    return <ScanErrorState kind={errorKind} onRetry={reset} />;
  }

  if (view === 'result' && restaurant) {
    return <VerifiedRestaurantResult onContinue={() => setView('write')} restaurant={restaurant} />;
  }

  if (view === 'write' && restaurant) {
    return (
      <VerifiedReviewForm
        onBack={() => setView('result')}
        onRatingsChange={setRatings}
        onSubmit={handleSubmit}
        canAddPhotos={photos.canAdd}
        onAddPhotos={() => void photos.add()}
        onRemovePhoto={photos.remove}
        onRetryPhoto={photos.retry}
        onTextChange={setText}
        photoError={photoErrorMessage(photos.items)}
        photos={photos.items}
        photosBusy={photos.isBusy}
        ratings={ratings}
        restaurant={restaurant}
        submitError={submitError}
        submitting={createMutation.isPending}
        text={text}
      />
    );
  }

  if (view === 'success' && restaurant) {
    const restaurantId = restaurant.id;
    return (
      <ScanSuccessState
        onGoHome={() => {
          reset();
          router.navigate('/home' as Href);
        }}
        onViewReview={() => {
          reset();
          router.navigate({ pathname: '/restaurants/[id]', params: { id: restaurantId, tab: 'reviews' } });
        }}
      />
    );
  }

  return (
    <ScannerView
      busy={resolveMutation.isPending}
      cameraActive={focused && view === 'scanner'}
      onScanned={handleScanned}
    />
  );
}
