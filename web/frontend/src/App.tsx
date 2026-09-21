import { Link, NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { CartPage } from './pages/CartPage';
import { CataloguePage } from './pages/CataloguePage';
import { MyOrdersPage } from './pages/MyOrdersPage';
import { SignInPage } from './pages/SignInPage';
import { AdminPage } from './pages/admin/AdminPage';
import { useCart } from './state/CartContext';
import { useSession } from './state/SessionContext';
import type { ReactNode } from 'react';

/**
 * Hides a route from someone who may not use it. This is convenience, not security —
 * the API enforces the same rules, and it is the only place that can.
 */
function RequireAuth({ children, adminOnly = false }: { children: ReactNode; adminOnly?: boolean }) {
  const { session, isAdmin } = useSession();
  const location = useLocation();

  if (!session) {
    return <Navigate to={`/login?next=${encodeURIComponent(location.pathname)}`} replace />;
  }

  if (adminOnly && !isAdmin) {
    return (
      <div className="card">
        <h2>Not your page</h2>
        <p className="hint">This area is for administrators. <Link to="/">Back to the shop</Link>.</p>
      </div>
    );
  }

  return <>{children}</>;
}

function Masthead() {
  const { session, isAdmin, signOut } = useSession();
  const cart = useCart();

  return (
    <header className="masthead">
      <div>
        <h1><Link to="/" className="brand">OrderFlow</Link></h1>
        <p className="subtitle">Order and inventory management</p>
      </div>

      <nav className="mainnav">
        <NavLink to="/" end>Shop</NavLink>
        <NavLink to="/cart">
          Cart{cart.itemCount > 0 && <span className="count">{cart.itemCount}</span>}
        </NavLink>
        {session && <NavLink to="/orders">My orders</NavLink>}
        {isAdmin && <NavLink to="/admin">Admin</NavLink>}

        {session ? (
          <span className="who">
            {session.username} <button className="link" onClick={signOut}>Sign out</button>
          </span>
        ) : (
          <NavLink to="/login">Sign in</NavLink>
        )}
      </nav>
    </header>
  );
}

export default function App() {
  return (
    <div className="app">
      <Masthead />

      <Routes>
        <Route path="/" element={<CataloguePage />} />
        <Route path="/cart" element={<CartPage />} />
        <Route path="/login" element={<SignInPage mode="signIn" />} />
        <Route path="/register" element={<SignInPage mode="register" />} />
        <Route path="/orders" element={<RequireAuth><MyOrdersPage /></RequireAuth>} />
        <Route path="/admin" element={<RequireAuth adminOnly><AdminPage /></RequireAuth>} />
        <Route
          path="*"
          element={
            <div className="card">
              <h2>Page not found</h2>
              <p className="hint"><Link to="/">Back to the shop</Link>.</p>
            </div>
          }
        />
      </Routes>
    </div>
  );
}
