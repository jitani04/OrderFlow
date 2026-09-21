import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { Order, Product } from '../types';

interface Line {
  productId: string;
  quantity: number;
}

export function PlaceOrderPanel({ onOrderPlaced }: { onOrderPlaced: () => void }) {
  const [products, setProducts] = useState<Product[]>([]);
  const [customerName, setCustomerName] = useState('Ada Lovelace');
  const [lines, setLines] = useState<Line[]>([{ productId: '', quantity: 1 }]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<Order | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const loaded = await api.listProducts();
        setProducts(loaded);
        setLines((current) =>
          current.map((line) => (line.productId ? line : { ...line, productId: loaded[0]?.id ?? '' })),
        );
      } catch {
        setError('Could not load products.');
      }
    })();
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setResult(null);

    try {
      // Only product and quantity are sent. The API reads the price from the catalogue,
      // so the browser cannot influence what the order is charged.
      // The name is honoured because this panel is admin-only; a customer placing their
      // own order always has it filed under their account name instead.
      const order = await api.placeOrder(
        lines
          .filter((line) => line.productId && line.quantity > 0)
          .map((line) => ({ productId: line.productId, quantity: line.quantity })),
        customerName,
      );

      setResult(order);
      onOrderPlaced();

      // Stock has moved, so the quantities shown in the dropdown are now stale.
      setProducts(await api.listProducts());
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not place the order.');
    } finally {
      setBusy(false);
    }
  }

  function updateLine(index: number, patch: Partial<Line>) {
    setLines((current) => current.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  }

  const estimatedTotal = lines.reduce((sum, line) => {
    const product = products.find((candidate) => candidate.id === line.productId);
    return sum + (product?.price ?? 0) * line.quantity;
  }, 0);

  return (
    <div className="card">
      <h2>Record an order</h2>
      <p className="hint">
        For an order taken on a customer's behalf. Stock is checked and deducted in one
        transaction, so the answer below is final — there is no pending state to wait through.
      </p>

      {error && <div className="alert error">{error}</div>}

      {result && (
        <div className={`alert ${result.status === 'Confirmed' ? 'success' : 'error'}`}>
          <strong>
            Order {result.id.slice(0, 8)} {result.status === 'Confirmed' ? 'confirmed' : 'rejected'}
          </strong>
          {result.status === 'Confirmed'
            ? ` — ${result.items.length} line(s), total ${result.totalAmount.toFixed(2)}.`
            : ''}
          {result.rejectionReason && <div style={{ marginTop: 4 }}>{result.rejectionReason}</div>}
        </div>
      )}

      <form onSubmit={submit}>
        <div className="field">
          <label htmlFor="customer">Customer</label>
          <input id="customer" value={customerName} onChange={(e) => setCustomerName(e.target.value)} />
        </div>

        {lines.map((line, index) => (
          <div className="row" key={index} style={{ marginBottom: 10 }}>
            <div>
              <label htmlFor={`product-${index}`}>Product</label>
              <select
                id={`product-${index}`}
                value={line.productId}
                onChange={(event) => updateLine(index, { productId: event.target.value })}
              >
                {products.map((product) => (
                  <option key={product.id} value={product.id}>
                    {product.sku} — {product.name} ({product.quantityOnHand} on hand)
                  </option>
                ))}
              </select>
            </div>

            <div className="shrink" style={{ width: 110 }}>
              <label htmlFor={`qty-${index}`}>Quantity</label>
              <input
                id={`qty-${index}`}
                type="number"
                min={1}
                value={line.quantity}
                onChange={(event) => updateLine(index, { quantity: Number(event.target.value) })}
              />
            </div>

            <div className="shrink">
              <button
                type="button"
                className="link"
                disabled={lines.length === 1}
                onClick={() => setLines((current) => current.filter((_, i) => i !== index))}
              >
                Remove
              </button>
            </div>
          </div>
        ))}

        <div className="toolbar" style={{ marginTop: 12 }}>
          <button
            type="button"
            className="link"
            onClick={() => setLines((current) => [...current, { productId: products[0]?.id ?? '', quantity: 1 }])}
          >
            Add line
          </button>
          <div className="spacer" />
          <span className="muted">Estimated total {estimatedTotal.toFixed(2)}</span>
          <button className="primary" type="submit" disabled={busy}>
            {busy ? 'Placing…' : 'Place order'}
          </button>
        </div>
      </form>
    </div>
  );
}
