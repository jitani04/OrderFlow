export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected';

export interface Product {
  id: string;
  sku: string;
  name: string;
  price: number;
  quantityOnHand: number;
  lowStockThreshold: number;
  isLow: boolean;
  createdAt: string;
  /** Echoed back in If-Match when changing stock, so a stale edit is refused. */
  version: string;
}

export interface OrderItem {
  productId: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: string;
  customerName: string;
  status: OrderStatus;
  totalAmount: number;
  createdAt: string;
  rejectionReason: string | null;
  items: OrderItem[];
}

export interface Session {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  username: string;
  role: string;
}

export interface CreateProductPayload {
  sku: string;
  name: string;
  price: number;
  quantityOnHand: number;
  lowStockThreshold: number;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface OrderQuery {
  page?: number;
  pageSize?: number;
  status?: OrderStatus | 'All';
}
