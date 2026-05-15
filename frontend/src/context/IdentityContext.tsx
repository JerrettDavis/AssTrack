import React, { createContext, useContext } from 'react';
import { useIdentity } from '../hooks/useIdentity';

interface IdentityContextType {
  roles: string[];
  accessTier: string;
  capabilities: string[];
  isOperator: boolean;
  isAdmin: boolean;
  loading: boolean;
}

const IdentityContext = createContext<IdentityContextType>({
  roles: [],
  accessTier: 'community',
  capabilities: [],
  isOperator: false,
  isAdmin: false,
  loading: true,
});

export function IdentityProvider({ children }: { children: React.ReactNode }) {
  const identity = useIdentity();
  return (
    <IdentityContext.Provider value={identity}>
      {children}
    </IdentityContext.Provider>
  );
}

export function useIdentityContext(): IdentityContextType {
  return useContext(IdentityContext);
}
