import React, { createContext, useContext } from 'react';
import { useIdentity } from '../hooks/useIdentity';

interface IdentityContextType {
  roles: string[];
  isOperator: boolean;
  loading: boolean;
}

const IdentityContext = createContext<IdentityContextType>({
  roles: [],
  isOperator: false,
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
