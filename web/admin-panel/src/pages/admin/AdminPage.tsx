import { useCallback, useState } from 'react';
import { OrdersPanel } from '../../components/OrdersPanel';
import { PlaceOrderPanel } from '../../components/PlaceOrderPanel';
import { ProductsPanel } from '../../components/ProductsPanel';

type Tab = 'orders' | 'products' | 'place';

const TABS: { id: Tab; label: string }[] = [
  { id: 'orders', label: 'All orders' },
  { id: 'products', label: 'Products & stock' },
  { id: 'place', label: 'Record an order' },
];

export function AdminPage() {
  const [tab, setTab] = useState<Tab>('orders');
  const [refreshToken, setRefreshToken] = useState(0);
  const bumpRefresh = useCallback(() => setRefreshToken((token) => token + 1), []);

  return (
    <>
      <div className="page-head">
        <h2>Admin</h2>
        <p className="hint">Every order in the system, and the catalogue behind the shop.</p>
      </div>

      <nav className="tabs">
        {TABS.map((entry) => (
          <button key={entry.id} aria-current={tab === entry.id} onClick={() => setTab(entry.id)}>
            {entry.label}
          </button>
        ))}
      </nav>

      {tab === 'orders' && (
        <OrdersPanel
          refreshToken={refreshToken}
          title="All orders"
          hint="Every customer's orders. Rejected ones are kept — they record what people tried to buy and could not get."
        />
      )}
      {tab === 'products' && <ProductsPanel isAdmin onChanged={bumpRefresh} />}
      {tab === 'place' && <PlaceOrderPanel onOrderPlaced={bumpRefresh} />}
    </>
  );
}
