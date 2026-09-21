import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { useCart } from '../state/CartContext';
import { useSession } from '../state/SessionContext';
import type { Order } from '../types';

export function CartPage() {
  const cart = useCart();
  const { session } = useSession();
  const navigate = useNavigate();
  const [placing, setPlacing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<Order | null>(null);

  async function checkout() {
    setPlacing(true);
    setError(null);

    try {
      // Only product and quantity are sent; the API reads the price from the catalogue,
      // so nothing here decides what the order costs.
      const order = await api.placeOrder(
        cart.lines.map((line) => ({ productId: line.productId, quantity: line.quantity })),
      );

      setResult(order);

      // Keep a rejected basket so the shopper can adjust quantities and try again.
      if (order.status === 'Confirmed') {
        cart.clear();
      }
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not place the order.');
    } finally {
      setPlacing(false);
    }
  }

  if (result) {
    const confirmed = result.status === 'Confirmed';

    return (
      <div className="card">
        <h2>{confirmed ? 'Order confirmed' : 'Order rejected'}</h2>

        <div className={`alert ${confirmed ? 'success' : 'error'}`}>
          <strong>Order {result.id.slice(0, 8)}</strong>
          {confirmed
            ? ` — ${result.items.length} line(s), total ${result.totalAmount.toFixed(2)}.`
            : ''}
          {result.rejectionReason && <div style={{ marginTop: 4 }}>{result.rejectionReason}</div>}
        </div>

        <div className="toolbar">
          <button className="link" onClick={() => { setResult(null); navigate('/'); }}>
            Back to the shop
          </button>
          {confirmed && (
            <button className="primary" onClick={() => navigate('/orders')}>
              View my orders
            </button>
          )}
        </div>
      </div>
    );
  }

  if (cart.lines.length === 0) {
    return (
      <div className="card">
        <h2>Your cart</h2>
        <p className="empty">Nothing in your cart yet. <Link to="/">Browse the shop</Link>.</p>
      </div>
    );
  }

  return (
    <div className="card">
      <h2>Your cart</h2>
      <p className="hint">
        Stock is checked when you place the order, not now — so a popular item can still
        sell out between here and checkout.
      </p>

      {error && <div className="alert error">{error}</div>}

      <table>
        <thead>
          <tr>
            <th>SKU</th>
            <th>Item</th>
            <th className="num">Price</th>
            <th className="num">Quantity</th>
            <th className="num">Line total</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {cart.lines.map((line) => (
            <tr key={line.productId}>
              <td className="mono">{line.sku}</td>
              <td>{line.name}</td>
              <td className="num">{line.price.toFixed(2)}</td>
              <td className="num">
                <input
                  type="number"
                  min={1}
                  style={{ width: 80 }}
                  aria-label={`Quantity for ${line.sku}`}
                  value={line.quantity}
                  onChange={(event) => cart.setQuantity(line.productId, Number(event.target.value))}
                />
              </td>
              <td className="num">{(line.price * line.quantity).toFixed(2)}</td>
              <td>
                <button className="link" onClick={() => cart.remove(line.productId)}>Remove</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="toolbar" style={{ marginTop: 16 }}>
        <button className="link" onClick={cart.clear}>Empty cart</button>
        <div className="spacer" />
        <strong style={{ marginRight: 12 }}>Total {cart.total.toFixed(2)}</strong>

        {session ? (
          <button className="primary" disabled={placing} onClick={() => void checkout()}>
            {placing ? 'Placing…' : 'Place order'}
          </button>
        ) : (
          <button className="primary" onClick={() => navigate('/login?next=/cart')}>
            Sign in to check out
          </button>
        )}
      </div>
    </div>
  );
}
