import { useCallback, useEffect, useState } from 'react';
import { clearSession, loadSession, setUnauthorizedHandler } from './api/client';
import { LoginForm } from './components/LoginForm';
import { OrdersPanel } from './components/OrdersPanel';
import { PlaceOrderPanel } from './components/PlaceOrderPanel';
import { ProductsPanel } from './components/ProductsPanel';
import type { Session } from './types';

type Tab = 'order' | 'orders' | 'products';

const TABS: { id: Tab; label: string }[] = [
  { id: 'order', label: 'Place order' },
  { id: 'orders', label: 'Orders' },
  { id: 'products', label: 'Products & stock' },
];

export default function App() {
  const [session, setSession] = useState<Session | null>(() => loadSession());
  const [tab, setTab] = useState<Tab>('order');

  // Bumped whenever something changes stock or orders, so the other tabs reload rather
  // than showing a figure that is already out of date.
  const [refreshToken, setRefreshToken] = useState(0);
  const bumpRefresh = useCallback(() => setRefreshToken((token) => token + 1), []);

  useEffect(() => {
    // One place decides what an expired token means: back to the login screen.
    setUnauthorizedHandler(() => setSession(null));
  }, []);

  const signOut = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  if (!session) {
    return <LoginForm onSignedIn={setSession} />;
  }

  return (
    <div className="app">
      <header className="masthead">
        <div>
          <h1>OrderFlow admin</h1>
          <p className="subtitle">Order and inventory management</p>
        </div>
        <div className="who">
          Signed in as <strong>{session.username}</strong> ({session.role}){' '}
          <button className="link" onClick={signOut}>Sign out</button>
        </div>
      </header>

      <nav className="tabs">
        {TABS.map((entry) => (
          <button key={entry.id} aria-current={tab === entry.id} onClick={() => setTab(entry.id)}>
            {entry.label}
          </button>
        ))}
      </nav>

      {tab === 'order' && <PlaceOrderPanel onOrderPlaced={bumpRefresh} />}
      {tab === 'orders' && <OrdersPanel refreshToken={refreshToken} />}
      {tab === 'products' && (
        <ProductsPanel isAdmin={session.role === 'Admin'} onChanged={bumpRefresh} />
      )}
    </div>
  );
}
