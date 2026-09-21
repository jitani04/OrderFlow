import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useSession } from '../state/SessionContext';

export function SignInPage({ mode }: { mode: 'signIn' | 'register' }) {
  const { signIn, register } = useSession();
  const navigate = useNavigate();
  const [params] = useSearchParams();

  const isRegister = mode === 'register';
  const [username, setUsername] = useState(isRegister ? '' : 'customer');
  const [password, setPassword] = useState(isRegister ? '' : 'customer123');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError(null);

    try {
      await (isRegister ? register(username, password) : signIn(username, password));

      // Return to whatever sent us here — usually the cart — rather than always home.
      navigate(params.get('next') ?? '/', { replace: true });
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'That did not work.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="login-shell">
      <div className="card">
        <h2>{isRegister ? 'Create an account' : 'Sign in'}</h2>
        <p className="hint">
          {isRegister
            ? 'A new account can shop and see its own orders.'
            : 'Sign in to place an order and see your order history.'}
        </p>

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
              autoComplete={isRegister ? 'new-password' : 'current-password'}
            />
            {isRegister && <p className="hint" style={{ marginTop: 6 }}>At least 8 characters.</p>}
          </div>

          <button className="primary" type="submit" disabled={busy}>
            {busy ? 'Working…' : isRegister ? 'Create account' : 'Sign in'}
          </button>
        </form>

        <p className="hint" style={{ marginTop: 16, marginBottom: 0 }}>
          {isRegister ? (
            <>Already have an account? <Link to="/login">Sign in</Link>.</>
          ) : (
            <>
              No account? <Link to="/register">Create one</Link>.
              <br />
              Demo accounts: <span className="mono">customer / customer123</span> ·{' '}
              <span className="mono">admin / admin123</span>
            </>
          )}
        </p>
      </div>
    </div>
  );
}
