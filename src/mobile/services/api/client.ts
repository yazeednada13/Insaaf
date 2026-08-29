import axios from 'axios';

import { API_BASE_URL } from '@/config/env';

const isDev = process.env.NODE_ENV !== 'production';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: {
    Accept: 'application/json',
    'Content-Type': 'application/json',
  },
});

if (isDev) {
  apiClient.interceptors.request.use((config) => {
    console.debug('[api] request', config.method?.toUpperCase(), config.url);
    return config;
  });

  apiClient.interceptors.response.use(
    (response) => {
      console.debug('[api] response', response.status, response.config.url);
      return response;
    },
    (error) => {
      console.debug('[api] error', error.message);
      return Promise.reject(error);
    },
  );
}
