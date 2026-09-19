import axios, { AxiosError, type AxiosInstance } from 'axios';
import type {
  CreateProductPayload,
  Order,
  OrderQuery,
  PagedResponse,
  Product,
  Session,
} from '../types';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5100';
const SESSION_KEY = 'orderflow.session';

export function loadSession(): Session | null {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) return null;

  try {
    const session = JSON.parse(raw) as Session;
    // Drop a token that has already expired rather than sending it and taking a 401.
    return new Date(session.expiresAt) > new Date() ? session : null;
  } catch {
    return null;
  }
}

export function saveSession(session: Session): void {
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function clearSession(): void {
  localStorage.removeItem(SESSION_KEY);
}

/** Lets the app drop back to the login screen the moment a token stops working. */
let onUnauthorized: (() => void) | null = null;

export function setUnauthorizedHandler(handler: () => void): void {
  onUnauthorized = handler;
}

const http: AxiosInstance = axios.create({ baseURL: BASE_URL });

// Request interceptor: attach the bearer token to every call. Doing it here rather than at
// each call site means no request can accidentally be sent unauthenticated.
http.interceptors.request.use((config) => {
  const session = loadSession();

  if (session) {
    config.headers.Authorization = `Bearer ${session.accessToken}`;
  }

  return config;
});

// Response interceptor: turn transport errors into readable messages, and treat a 401 as
// "this session is over" in exactly one place.
http.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401 && !error.config?.url?.endsWith('/auth/login')) {
      clearSession();
      onUnauthorized?.();
      return Promise.reject(new Error('Your session has expired. Please sign in again.'));
    }

    return Promise.reject(new Error(describeFailure(error)));
  },
);

/** Turns an RFC 7807 problem document into something worth showing a person. */
function describeFailure(error: AxiosError): string {
  const problem = error.response?.data as
    | { errors?: Record<string, string[]>; detail?: string; title?: string }
    | undefined;

  if (problem?.errors) {
    const messages = Object.values(problem.errors).flat();
    if (messages.length > 0) return messages.join(' ');
  }

  return (
    problem?.detail ??
    problem?.title ??
    error.message ??
    'The request failed.'
  );
}

export const api = {
  login: async (username: string, password: string) =>
    (await http.post<Session>('/auth/login', { username, password })).data,

  listProducts: async () => (await http.get<Product[]>('/api/products')).data,

  createProduct: async (payload: CreateProductPayload) =>
    (await http.post<Product>('/api/products', payload)).data,

  updateStock: async (
    productId: string,
    quantityOnHand: number,
    lowStockThreshold: number,
    version: string,
  ) =>
    (await http.put<Product>(
      `/api/products/${productId}/stock`,
      { quantityOnHand, lowStockThreshold },
      // The version this edit was based on. The API refuses the write if anything changed
      // since — an order deducting stock, or another admin saving first.
      { headers: { 'If-Match': `"${version}"` } },
    )).data,

  placeOrder: async (customerName: string, items: { productId: string; quantity: number }[]) =>
    (await http.post<Order>('/api/orders', { customerName, items })).data,

  listOrders: async (query: OrderQuery = {}) =>
    (await http.get<PagedResponse<Order>>('/api/orders', {
      params: {
        page: query.page ?? 1,
        pageSize: query.pageSize ?? 25,
        // 'All' is a UI concept; the API means "every status" by omitting the parameter.
        ...(query.status && query.status !== 'All' ? { status: query.status } : {}),
      },
    })).data,
};
