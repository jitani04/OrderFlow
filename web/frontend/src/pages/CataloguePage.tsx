import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useCart } from '../state/CartContext';
import type { Product } from '../types';

export function CataloguePage() {
  const [products, setProducts] = useState<Product[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [justAdded, setJustAdded] = useState<string | null>(null);
  const cart = useCart();

  useEffect(() => {
    // No token needed: the catalogue is public, so the shop renders before sign-in.
    api.listProducts().then(
      (loaded) => {
        setProducts(loaded);
        setLoading(false);
      },
      () => {
        setError('Could not load the catalogue.');
        setLoading(false);
      },
    );
  }, []);

  function addToCart(product: Product) {
    cart.add(product, 1);
    setJustAdded(product.id);
    window.setTimeout(() => setJustAdded((current) => (current === product.id ? null : current)), 1500);
  }

  if (loading) {
    return <div className="card"><p className="empty">Loading the catalogue…</p></div>;
  }

  return (
    <>
      <div className="page-head">
        <h2>Shop</h2>
        <p className="hint">
          Stock shown is live. An order is filled completely or not at all, so if one line
          is short nothing is reserved.
        </p>
      </div>

      {error && <div className="alert error">{error}</div>}

      <div className="product-grid">
        {products.map((product) => {
          const soldOut = product.quantityOnHand === 0;

          return (
            <article className="product" key={product.id}>
              <div className="product-head">
                <span className="mono muted">{product.sku}</span>
                {product.isLow && !soldOut && <span className="badge low">Low stock</span>}
                {soldOut && <span className="badge rejected">Sold out</span>}
              </div>

              <h3>{product.name}</h3>

              <div className="product-foot">
                <span className="price">{product.price.toFixed(2)}</span>
                <span className="muted">{product.quantityOnHand} available</span>
              </div>

              <button
                className="primary"
                disabled={soldOut}
                onClick={() => addToCart(product)}
              >
                {justAdded === product.id ? 'Added ✓' : soldOut ? 'Sold out' : 'Add to cart'}
              </button>
            </article>
          );
        })}
      </div>

      {products.length === 0 && !error && (
        <div className="card"><p className="empty">Nothing in the catalogue yet.</p></div>
      )}

      {cart.itemCount > 0 && (
        <div className="alert info" style={{ marginTop: 20 }}>
          {cart.itemCount} item{cart.itemCount === 1 ? '' : 's'} in your cart —{' '}
          <Link to="/cart">go to checkout</Link>
        </div>
      )}
    </>
  );
}
