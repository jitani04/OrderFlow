import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { api, clearSession, loadSession, saveSession, setUnauthorizedHandler } from '../api/client';
import type { Session } from '../types';

interface SessionState {
  session: Session | null;
  isAdmin: boolean;
  signIn: (username: string, password: string) => Promise<void>;
  register: (username: string, password: string) => Promise<void>;
  signOut: () => void;
}

const SessionContext = createContext<SessionState | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => loadSession());

  useEffect(() => {
    // One place decides what an expired token means, wherever in the app it surfaces.
    setUnauthorizedHandler(() => setSession(null));
  }, []);

  const signIn = useCallback(async (username: string, password: string) => {
    const next = await api.login(username, password);
    saveSession(next);
    setSession(next);
  }, []);

  const register = useCallback(async (username: string, password: string) => {
    const next = await api.register(username, password);
    saveSession(next);
    setSession(next);
  }, []);

  const signOut = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  const value = useMemo<SessionState>(
    () => ({ session, isAdmin: session?.role === 'Admin', signIn, register, signOut }),
    [session, signIn, register, signOut],
  );

  return <SessionContext value={value}>{children}</SessionContext>;
}

export function useSession(): SessionState {
  const value = useContext(SessionContext);

  if (!value) {
    throw new Error('useSession must be used inside a SessionProvider.');
  }

  return value;
}
