import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';
import * as ImagePicker from 'expo-image-picker';
import { useCallback, useEffect, useRef, useState } from 'react';

import * as verificationApi from '@/services/verification/verificationApi';
import { VerificationApiError } from '@/services/verification/verificationApi';
import { randomUuid } from '@/utils/uuid';

/** Matches the server: at most 5 photos per review (Phase 9A, D-3). */
export const MAX_REVIEW_PHOTOS = 5;

// Compressed on the device before upload (plan section 9); the server re-encodes again.
const MAX_SIDE = 2048;
const JPEG_QUALITY = 0.8;
const RETRY_DELAY_MS = 2000;
const MAX_RETRIES = 3;
// Server answers that mean "the same upload is still in progress, try again shortly".
const RETRYABLE = new Set(['upload_in_progress', 'try_again', 'network_error']);

export type PhotoItemStatus = 'compressing' | 'uploading' | 'uploaded' | 'failed';

export interface PhotoItem {
  /** Local key for the list. */
  key: string;
  /** One id per logical photo, reused on every retry, so a lost response never makes a duplicate. */
  clientUploadId: string;
  /** The compressed local file; null while compressing or when compression failed. */
  localUri: string | null;
  status: PhotoItemStatus;
  /** The server's photo id once uploaded. */
  photoId: string | null;
  errorCode: string | null;
}

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

async function compress(asset: ImagePicker.ImagePickerAsset): Promise<string> {
  const context = ImageManipulator.manipulate(asset.uri);
  const longest = Math.max(asset.width, asset.height);
  if (longest > MAX_SIDE) {
    context.resize(asset.width >= asset.height ? { width: MAX_SIDE } : { height: MAX_SIDE });
  }

  const image = await context.renderAsync();
  const result = await image.saveAsync({ compress: JPEG_QUALITY, format: SaveFormat.JPEG });
  return result.uri;
}

/**
 * The review form's photos (Phase 9A, task 9A.5). Each photo uploads on its own as soon as it's
 * picked; one failure never cancels another. Only uploaded photos are sent with the review, and the
 * visit token is redeemed only by the review itself, never by an upload.
 */
export function useReviewPhotos(token: string | null) {
  const [items, setItems] = useState<PhotoItem[]>([]);
  const itemsRef = useRef<PhotoItem[]>([]);
  useEffect(() => {
    itemsRef.current = items;
  }, [items]);

  const update = useCallback((key: string, patch: Partial<PhotoItem>) => {
    setItems((current) => current.map((item) => (item.key === key ? { ...item, ...patch } : item)));
  }, []);

  const upload = useCallback(
    async (key: string, localUri: string, clientUploadId: string) => {
      if (!token) {
        return;
      }

      let uploadId = clientUploadId;
      update(key, { status: 'uploading', errorCode: null, clientUploadId: uploadId });
      for (let attempt = 0; ; attempt++) {
        try {
          const photo = await verificationApi.uploadPhoto(token, uploadId, {
            uri: localUri,
            name: `${uploadId}.jpg`,
            type: 'image/jpeg',
          });
          update(key, { status: 'uploaded', photoId: photo.id, clientUploadId: uploadId });
          return;
        } catch (error) {
          const code = error instanceof VerificationApiError ? error.code : 'unknown';
          if (code === 'photo_removed' && attempt < MAX_RETRIES) {
            // The earlier upload was removed: this is a new logical upload.
            uploadId = randomUuid();
            update(key, { clientUploadId: uploadId });
            continue;
          }

          if (RETRYABLE.has(code) && attempt < MAX_RETRIES) {
            await wait(RETRY_DELAY_MS);
            continue;
          }

          update(key, { status: 'failed', photoId: null, errorCode: code });
          return;
        }
      }
    },
    [token, update],
  );

  const add = useCallback(async () => {
    const remaining = MAX_REVIEW_PHOTOS - itemsRef.current.length;
    if (remaining <= 0 || !token) {
      return;
    }

    const picked = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsMultipleSelection: true,
      selectionLimit: remaining,
      orderedSelection: true,
      exif: false,
      quality: 1,
    });
    if (picked.canceled) {
      return;
    }

    const assets = picked.assets.slice(0, remaining);
    const added = assets.map<PhotoItem>(() => ({
      key: randomUuid(),
      clientUploadId: randomUuid(),
      localUri: null,
      status: 'compressing',
      photoId: null,
      errorCode: null,
    }));
    setItems((current) => [...current, ...added].slice(0, MAX_REVIEW_PHOTOS));

    await Promise.all(assets.map(async (asset, index) => {
      const item = added[index];
      let localUri: string;
      try {
        localUri = await compress(asset);
      } catch {
        update(item.key, { status: 'failed', errorCode: 'compress_failed' });
        return;
      }

      update(item.key, { localUri });
      await upload(item.key, localUri, item.clientUploadId);
    }));
  }, [token, update, upload]);

  /** Re-uploads the same local file with the same clientUploadId. */
  const retry = useCallback(
    (key: string) => {
      const item = itemsRef.current.find((candidate) => candidate.key === key);
      if (item?.status === 'failed' && item.localUri) {
        void upload(key, item.localUri, item.clientUploadId);
      }
    },
    [upload],
  );

  /** Drops the photo now; an uploaded one is also deleted on the server, best effort. */
  const remove = useCallback((key: string) => {
    const item = itemsRef.current.find((candidate) => candidate.key === key);
    setItems((current) => current.filter((candidate) => candidate.key !== key));
    if (item?.photoId) {
      // If this fails, the server's cleanup removes the photo and frees its slot later.
      verificationApi.deletePhoto(item.photoId).catch(() => undefined);
    }
  }, []);

  /** After invalid_photo: the photos sent with the review must be uploaded again or left out. */
  const markFailed = useCallback((photoIds: string[]) => {
    const sent = new Set(photoIds);
    setItems((current) =>
      current.map((item) =>
        item.photoId && sent.has(item.photoId)
          ? { ...item, status: 'failed', photoId: null, errorCode: 'invalid_photo' }
          : item,
      ),
    );
  }, []);

  const reset = useCallback(() => setItems([]), []);

  return {
    items,
    add,
    retry,
    remove,
    markFailed,
    reset,
    canAdd: items.length < MAX_REVIEW_PHOTOS,
    isBusy: items.some((item) => item.status === 'compressing' || item.status === 'uploading'),
    failedCount: items.filter((item) => item.status === 'failed').length,
    uploadedIds: items.filter((item) => item.status === 'uploaded' && item.photoId).map((item) => item.photoId!),
  };
}
