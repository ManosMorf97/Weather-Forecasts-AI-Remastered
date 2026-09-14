import { useCallback, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { ID } from 'appwrite';
import { account } from './appwriteClient';
import { createProfile, UnauthorizedError } from '../api/profileApi';
import { AuthContext } from './authContextValue';
import type { AuthContextValue, AuthStatus } from './authContextValue';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [hasCitySiteSelection, setHasCitySiteSelection] = useState<boolean | null>(null);
  const [profileError, setProfileError] = useState<string | null>(null);

  // UC2: mint a fresh JWT and ask the backend to JIT-provision the profile row, then
  // report back whether the user still needs to complete city/service selection.
  const syncProfile = useCallback(async () => {
    setProfileError(null);
    try {
      const { jwt } = await account.createJWT();
      const profile = await createProfile(jwt);
      setHasCitySiteSelection(profile.hasCitySiteSelection);
      setStatus('authenticated');
    } catch (err) {
      if (err instanceof UnauthorizedError) {
        // E2: token verification failed - treat the user as logged out.
        setStatus('unauthenticated');
        setHasCitySiteSelection(null);
        return;
      }
      // E1: provisioning failed - stay authenticated and let the user retry.
      setHasCitySiteSelection(null);
      setProfileError('Could not load your profile. Please retry.');
      setStatus('authenticated');
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        // UC1 steps 2-3: restore any existing session from local persistence.
        await account.get();
        if (!cancelled) {
          await syncProfile();
        }
      } catch {
        if (!cancelled) {
          setStatus('unauthenticated');
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [syncProfile]);

  const login = useCallback(
    async (email: string, password: string) => {
      await account.createEmailPasswordSession({ email, password });
      await syncProfile();
    },
    [syncProfile],
  );

  const register = useCallback(
    async (name: string, email: string, password: string) => {
      await account.create({ userId: ID.unique(), email, password, name });
      await account.createEmailPasswordSession({ email, password });
      await syncProfile();
    },
    [syncProfile],
  );

  const logout = useCallback(async () => {
    await account.deleteSession({ sessionId: 'current' });
    setStatus('unauthenticated');
    setHasCitySiteSelection(null);
  }, []);

  const value: AuthContextValue = {
    status,
    hasCitySiteSelection,
    profileError,
    login,
    register,
    logout,
    retryProfileSync: () => {
      void syncProfile();
    },
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
