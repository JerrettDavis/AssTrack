import { useState, useEffect } from 'react';
import { apiGet } from '../api/client';

interface Identity {
  roles: string[];
  accessTier: string;
  capabilities: string[];
  isOperator: boolean;
  isAdmin: boolean;
  loading: boolean;
}

interface IdentityResponse {
  roles: string[];
  accessTier?: string;
  capabilities?: string[];
}

export function useIdentity(): Identity {
  const [identity, setIdentity] = useState<Identity>({ roles: [], accessTier: 'community', capabilities: [], isOperator: false, isAdmin: false, loading: true });

  useEffect(() => {
    apiGet<IdentityResponse>('/api/auth/me')
      .then(data => {
        const roles = data.roles ?? [];
        setIdentity({
          roles,
          accessTier: data.accessTier ?? 'community',
          capabilities: data.capabilities ?? [],
          isOperator: roles.includes('operator') || roles.includes('admin'),
          isAdmin: roles.includes('admin'),
          loading: false,
        });
      })
      .catch(() => {
        setIdentity({ roles: [], accessTier: 'community', capabilities: [], isOperator: false, isAdmin: false, loading: false });
      });
  }, []);

  return identity;
}
