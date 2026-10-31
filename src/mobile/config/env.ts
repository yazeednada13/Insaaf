/**
 * API base URL.
 * Defaults to the ASP.NET Core `http` launch profile (see launchSettings.json).
 * For a physical device, set EXPO_PUBLIC_API_BASE_URL to the computer's LAN address
 * when starting Expo, e.g. EXPO_PUBLIC_API_BASE_URL=http://192.168.1.20:5232.
 */
export const API_BASE_URL = process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://localhost:5232';
