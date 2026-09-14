import { createContext } from 'react';

export type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated';

export interface AuthContextValue {
  status: AuthStatus;
  hasCitySiteSelection: boolean | null;
  profileError: string | null;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  retryProfileSync: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
