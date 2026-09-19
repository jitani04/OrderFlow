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
