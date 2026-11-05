import { router, useFocusEffect } from 'expo-router';
import { useCallback, useRef, useState } from 'react';

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
import type { VerificationApiError } from '@/services/verification/verificationApi';
import type { ScanErrorKind, VerifiedRestaurantDto } from '@/types/verification';
import { extractVisitToken } from '@/utils/visitPayload';

type ScanView = 'scanner' | 'result' | 'write' | 'success' | 'error';

// Our copy for submit failures that keep the form: the token was not redeemed, so a retry is safe.
function submitErrorMessage(error: VerificationApiError): string {
  if (error.code === 'rate_limited') {
    return 'محاولات كثيرة. انتظر قليلًا ثم حاول مرة أخرى.';
  }

  if (error.code === 'network_error') {
    return 'تعذّر الاتصال بالخادم. تحقق من اتصالك بالإنترنت.';
  }

  return 'تعذّر إرسال التقييم. حاول مرة أخرى.';
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

  const handleSubmit = () => {
    if (!token || createMutation.isPending) {
      return;
    }

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
      },
      {
        onSuccess: () => setView('success'),
        onError: (error) => {
          if (error.kind === 'failed') {
            setSubmitError(submitErrorMessage(error));
          } else {
            showError(error.kind);
          }
        },
      },
    );
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
        onTextChange={setText}
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
          router.navigate('/restaurants');
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
