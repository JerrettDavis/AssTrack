import { useState, useEffect } from 'react';
import { apiGet } from '../api/client';

interface Identity {
  roles: string[];
  isOperator: boolean;
  loading: boolean;
}

interface IdentityResponse {
  roles: string[];
}

export function useIdentity(): Identity {
  const [identity, setIdentity] = useState<Identity>({ roles: [], isOperator: false, loading: true });

  useEffect(() => {
    apiGet<IdentityResponse>('/api/auth/me')
      .then(data => {
        const roles = data.roles ?? [];
        setIdentity({ roles, isOperator: roles.includes('operator'), loading: false });
      })
      .catch(() => {
        setIdentity({ roles: [], isOperator: false, loading: false });
      });
  }, []);

  return identity;
}
