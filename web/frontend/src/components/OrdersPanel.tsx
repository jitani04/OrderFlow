import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { Order, OrderStatus, PagedResponse } from '../types';

const STATUS_FILTERS: (OrderStatus | 'All')[] = ['All', 'Confirmed', 'Rejected', 'Pending'];
const PAGE_SIZE = 10;

export function OrdersPanel({
  refreshToken,
  title = 'Orders',
  hint = 'Rejected orders are kept, not discarded — they record what customers tried to buy and could not get.',
  showCustomer = true,
}: {
  refreshToken: number;
  title?: string;
  hint?: string;
  /** Hidden on a customer's own order list, where every row is them. */
  showCustomer?: boolean;
}) {
  const [result, setResult] = useState<PagedResponse<Order> | null>(null);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<OrderStatus | 'All'>('All');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const refresh = useCallback(async () => {
    setBusy(true);

    try {
      setResult(await api.listOrders({ page, pageSize: PAGE_SIZE, status }));
      setError(null);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not load orders.');
    } finally {
      setBusy(false);
    }
  }, [page, status]);

  useEffect(() => {
    void refresh();
  }, [refresh, refreshToken]);

  // A new filter almost always means fewer pages, so staying on page 7 would show an
  // empty table. Going back to the first page is what the user meant.
  function changeStatus(next: OrderStatus | 'All') {
    setStatus(next);
    setPage(1);
  }

  const orders = result?.items ?? [];

  return (
    <div className="card">
      <h2>{title}</h2>
      <p className="hint">{hint}</p>

      {error && <div className="alert error">{error}</div>}

      <div className="toolbar">
        {STATUS_FILTERS.map((option) => (
          <button
            key={option}
            className="link"
            style={status === option ? { borderColor: 'var(--accent)', color: 'var(--accent)' } : undefined}
            onClick={() => changeStatus(option)}
          >
            {option}
          </button>
        ))}
        <div className="spacer" />
        <button className="link" disabled={busy} onClick={() => void refresh()}>Refresh</button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Order</th>
            {showCustomer && <th>Customer</th>}
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
              {showCustomer && <td>{order.customerName}</td>}
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
              <td colSpan={showCustomer ? 7 : 6} className="empty">
                {busy ? 'Loading…' : 'No orders match this filter.'}
              </td>
            </tr>
          )}
        </tbody>
      </table>

      {result && result.totalCount > 0 && (
        <div className="toolbar" style={{ marginTop: 14, marginBottom: 0 }}>
          <span className="muted">
            Page {result.page} of {result.totalPages} — {result.totalCount} order
            {result.totalCount === 1 ? '' : 's'}
          </span>
          <div className="spacer" />
          <button
            className="link"
            disabled={!result.hasPreviousPage || busy}
            onClick={() => setPage((current) => current - 1)}
          >
            ‹ Previous
          </button>
          <button
            className="link"
            disabled={!result.hasNextPage || busy}
            onClick={() => setPage((current) => current + 1)}
          >
            Next ›
          </button>
        </div>
      )}
    </div>
  );
}
