import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { Product } from '../types';

export function ProductsPanel({ isAdmin, onChanged }: { isAdmin: boolean; onChanged: () => void }) {
  const [products, setProducts] = useState<Product[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [editing, setEditing] = useState<Record<string, { quantity: string; threshold: string }>>({});
  const [busyId, setBusyId] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setProducts(await api.listProducts());
      setError(null);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not load products.');
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function saveStock(product: Product) {
    const draft = editing[product.id];
    if (!draft) return;

    setBusyId(product.id);
    setError(null);
    setNotice(null);

    function dropEdit() {
      setEditing((current) => {
        const next = { ...current };
        delete next[product.id];
        return next;
      });
    }

    try {
      // product.version is what this edit was based on. If an order deducted stock, or
      // another admin saved first, the API refuses rather than letting this absolute count
      // overwrite a change nobody here has seen.
      await api.updateStock(
        product.id,
        Number(draft.quantity),
        Number(draft.threshold),
        product.version,
      );

      dropEdit();

      // Refresh before announcing success, or the banner appears next to the old numbers
      // for as long as the reload takes.
      await refresh();
      setNotice(`Stock updated for ${product.sku}.`);
      onChanged();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Update failed.');

      // A conflict means the row on screen is stale. Reload and drop the edit — leaving
      // the old numbers in the boxes would only invite the same failed save again.
      await refresh();
      dropEdit();
    } finally {
      setBusyId(null);
    }
  }

  return (
    <>
      <div className="card">
        <h2>Products and stock</h2>
        <p className="hint">
          A product is flagged low when quantity on hand reaches its threshold. Placing an
          order deducts from on hand immediately.
        </p>

        {error && <div className="alert error">{error}</div>}
        {notice && <div className="alert success">{notice}</div>}

        <div className="toolbar">
          <button className="link" onClick={() => void refresh()}>Refresh</button>
          <div className="spacer" />
          {!isAdmin && <span className="muted">Only an admin can change stock.</span>}
        </div>

        <table>
          <thead>
            <tr>
              <th>SKU</th>
              <th>Name</th>
              <th className="num">Price</th>
              <th className="num">On hand</th>
              <th className="num">Threshold</th>
              <th>Status</th>
              {isAdmin && <th>Set stock</th>}
            </tr>
          </thead>
          <tbody>
            {products.map((product) => {
              const draft = editing[product.id];

              return (
                <tr key={product.id}>
                  <td className="mono">{product.sku}</td>
                  <td>{product.name}</td>
                  <td className="num">{product.price.toFixed(2)}</td>
                  <td className="num">{product.quantityOnHand}</td>
                  <td className="num">{product.lowStockThreshold}</td>
                  <td>
                    <span className={`badge ${product.isLow ? 'low' : 'ok'}`}>
                      {product.isLow ? 'Low' : 'OK'}
                    </span>
                  </td>
                  {isAdmin && (
                    <td>
                      {draft ? (
                        <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                          <input
                            style={{ width: 70 }}
                            type="number"
                            min={0}
                            aria-label={`Quantity for ${product.sku}`}
                            value={draft.quantity}
                            onChange={(event) =>
                              setEditing((c) => ({
                                ...c,
                                [product.id]: { ...draft, quantity: event.target.value },
                              }))
                            }
                          />
                          <input
                            style={{ width: 70 }}
                            type="number"
                            min={0}
                            aria-label={`Threshold for ${product.sku}`}
                            value={draft.threshold}
                            onChange={(event) =>
                              setEditing((c) => ({
                                ...c,
                                [product.id]: { ...draft, threshold: event.target.value },
                              }))
                            }
                          />
                          <button
                            className="link"
                            disabled={busyId === product.id}
                            onClick={() => void saveStock(product)}
                          >
                            Save
                          </button>
                        </span>
                      ) : (
                        <button
                          className="link"
                          onClick={() =>
                            setEditing((c) => ({
                              ...c,
                              [product.id]: {
                                quantity: String(product.quantityOnHand),
                                threshold: String(product.lowStockThreshold),
                              },
                            }))
                          }
                        >
                          Edit
                        </button>
                      )}
                    </td>
                  )}
                </tr>
              );
            })}
            {products.length === 0 && (
              <tr>
                <td colSpan={isAdmin ? 7 : 6} className="empty">No products.</td>
              </tr>
            )}
          </tbody>
        </table>

        {isAdmin && <CreateProduct onCreated={() => { void refresh(); onChanged(); }} />}
      </div>
    </>
  );
}

function CreateProduct({ onCreated }: { onCreated: () => void }) {
  const [sku, setSku] = useState('');
  const [name, setName] = useState('');
  const [price, setPrice] = useState('0');
  const [quantity, setQuantity] = useState('0');
  const [threshold, setThreshold] = useState('0');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      await api.createProduct({
        sku,
        name,
        price: Number(price),
        quantityOnHand: Number(quantity),
        lowStockThreshold: Number(threshold),
      });

      setSku('');
      setName('');
      setPrice('0');
      setQuantity('0');
      setThreshold('0');
      onCreated();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Could not create the product.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <details className="creator">
      <summary>Add a product</summary>

      {error && <div className="alert error" style={{ marginTop: 12 }}>{error}</div>}

      <form onSubmit={submit} style={{ marginTop: 12 }}>
        <div className="row">
          <div>
            <label htmlFor="new-sku">SKU</label>
            <input id="new-sku" value={sku} onChange={(e) => setSku(e.target.value)} placeholder="OF-LAMP-01" />
          </div>
          <div>
            <label htmlFor="new-name">Name</label>
            <input id="new-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Desk Lamp" />
          </div>
          <div className="shrink" style={{ width: 110 }}>
            <label htmlFor="new-price">Price</label>
            <input id="new-price" type="number" min={0} step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} />
          </div>
          <div className="shrink" style={{ width: 110 }}>
            <label htmlFor="new-qty">On hand</label>
            <input id="new-qty" type="number" min={0} value={quantity} onChange={(e) => setQuantity(e.target.value)} />
          </div>
          <div className="shrink" style={{ width: 110 }}>
            <label htmlFor="new-threshold">Threshold</label>
            <input id="new-threshold" type="number" min={0} value={threshold} onChange={(e) => setThreshold(e.target.value)} />
          </div>
          <div className="shrink">
            <button className="primary" type="submit" disabled={busy}>
              {busy ? 'Adding…' : 'Add'}
            </button>
          </div>
        </div>
      </form>
    </details>
  );
}
