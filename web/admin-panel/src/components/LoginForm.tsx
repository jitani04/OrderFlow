import { useState, type FormEvent } from 'react';
import { api, saveSession } from '../api/client';
import type { Session } from '../types';

export function LoginForm({ onSignedIn }: { onSignedIn: (session: Session) => void }) {
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('admin123');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      const session = await api.login(username, password);
      saveSession(session);
      onSignedIn(session);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Sign in failed.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-shell">
      <div className="card">
        <h2>OrderFlow admin</h2>
        <p className="hint">Sign in to manage products and place orders.</p>

        {error && <div className="alert error">{error}</div>}

        <form onSubmit={submit}>
          <div className="field">
            <label htmlFor="username">Username</label>
            <input
              id="username"
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="username"
            />
          </div>

          <div className="field">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
            />
          </div>

          <button className="primary" type="submit" disabled={busy}>
            {busy ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <p className="hint" style={{ marginTop: 16, marginBottom: 0 }}>
          Seeded account: <span className="mono">admin / admin123</span>
        </p>
      </div>
    </div>
  );
}
