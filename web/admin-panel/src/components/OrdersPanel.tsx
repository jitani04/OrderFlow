import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Order } from '../types';

export function OrdersPanel({ refreshToken }: { refreshToken: number }) {
  const [orders, setOrders] = useState<Order[]>([]);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setOrders(await api.listOrders());
      setError(null);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not load orders.');
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh, refreshToken]);

  return (
    <div className="card">
      <h2>Orders</h2>
      <p className="hint">
        Rejected orders are kept, not discarded — they record what customers tried to buy
        and could not get.
      </p>

      {error && <div className="alert error">{error}</div>}

      <div className="toolbar">
        <button className="link" onClick={() => void refresh()}>Refresh</button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Order</th>
            <th>Customer</th>
            <th>Status</th>
            <th className="num">Lines</th>
            <th className="num">Total</th>
            <th>Placed</th>
            <th>Reason</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <tr key={order.id}>
              <td className="mono">{order.id.slice(0, 8)}</td>
              <td>{order.customerName}</td>
              <td>
                <span className={`badge ${order.status.toLowerCase()}`}>{order.status}</span>
              </td>
              <td className="num">{order.items.length}</td>
              <td className="num">{order.totalAmount.toFixed(2)}</td>
              <td className="muted">{new Date(order.createdAt).toLocaleString()}</td>
              <td className="muted">{order.rejectionReason ?? '—'}</td>
            </tr>
          ))}
          {orders.length === 0 && (
            <tr>
              <td colSpan={7} className="empty">No orders yet.</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
