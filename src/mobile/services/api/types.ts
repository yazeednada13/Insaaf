export interface ApiError {
  code: string;
  message: string;
}

export interface ApiMeta {
  requestId?: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  errors?: ApiError[];
  meta?: ApiMeta;
}
