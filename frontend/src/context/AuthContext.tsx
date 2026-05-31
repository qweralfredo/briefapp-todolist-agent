import React, { createContext, useContext, useEffect, useState } from 'react';
import { onAuthStateChanged, signInWithPopup, GoogleAuthProvider, signOut } from 'firebase/auth';
import type { User } from 'firebase/auth';
import { auth } from '../config/firebase';
import { setAuthToken, setApiKey } from '../api/client';

interface AuthContextType {
  user: User | any | null;
  loading: boolean;
  signInWithGoogle: () => Promise<void>;
  signInAsGuest: () => void;
  signInWithApiKey: (key: string) => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType>({} as AuthContextType);

export const useAuth = () => useContext(AuthContext);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | any | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const isDev = import.meta.env.VITE_MODE === 'dev';
    if (isDev) {
      setUser({ isGuest: true, uid: 'dev-user', email: 'dev@briefapp.local', displayName: 'Developer' });
      setLoading(false);
      return;
    }

    const isGuest = localStorage.getItem('isGuest') === 'true';
    if (isGuest) {
      setUser({ isGuest: true, uid: 'guest', email: 'guest@briefapp.local', displayName: 'Guest User' });
      setLoading(false);
      return;
    }

    const storedApiKey = localStorage.getItem('briefappApiKey');
    if (storedApiKey) {
      setApiKey(storedApiKey);
      setUser({ isApiKey: true, uid: 'api-key-user', email: 'apikey@briefapp.local', displayName: 'API Key User', apiKey: storedApiKey });
      setLoading(false);
      return;
    }

    const unsubscribe = onAuthStateChanged(auth, async (user) => {
      setUser(user);
      if (user) {
        const token = await user.getIdToken();
        setAuthToken(token);
      } else {
        setAuthToken(null);
      }
      setLoading(false);
    });
    return unsubscribe;
  }, []);

  const signInWithGoogle = async () => {
    const provider = new GoogleAuthProvider();
    await signInWithPopup(auth, provider);
  };

  const signInAsGuest = () => {
    localStorage.setItem('isGuest', 'true');
    setUser({ isGuest: true, uid: 'guest', email: 'guest@briefapp.local', displayName: 'Guest User' });
  };

  const signInWithApiKey = (key: string) => {
    localStorage.setItem('briefappApiKey', key);
    localStorage.removeItem('isGuest');
    setApiKey(key);
    setUser({ isApiKey: true, uid: 'api-key-user', email: 'apikey@briefapp.local', displayName: 'API Key User', apiKey: key });
  };

  const logout = async () => {
    localStorage.removeItem('isGuest');
    localStorage.removeItem('briefappApiKey');
    setApiKey(null);
    setAuthToken(null);
    await signOut(auth);
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, loading, signInWithGoogle, signInAsGuest, signInWithApiKey, logout }}>
      {!loading && children}
    </AuthContext.Provider>
  );
};
