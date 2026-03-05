/**
 * JWT Utility — decodes token payload and maps .NET ClaimTypes to readable names.
 *
 * The backend uses System.Security.Claims.ClaimTypes which serialize as long URIs:
 *   ClaimTypes.Role  → "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
 *   ClaimTypes.Email → "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"
 *
 * Custom claims (plain string keys): "tenantId", "userId"
 *
 * NOTE: firstName, lastName, tenantName are NOT in the JWT — they come from
 * the login response body and are stored separately in localStorage.
 */

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export interface JwtPayload {
  [key: string]: unknown;
}

export function decodeJwt(token: string): JwtPayload | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    return JSON.parse(atob(padded)) as JwtPayload;
  } catch {
    return null;
  }
}

export function getTokenPayload(): JwtPayload | null {
  const token = localStorage.getItem('token');
  if (!token) return null;
  return decodeJwt(token);
}

/** Read role from the JWT's ClaimTypes.Role claim */
export const getRole = (): string => {
  const payload = getTokenPayload();
  if (!payload) return '';
  return (payload[ROLE_CLAIM] as string) || '';
};

export const isAdmin = (): boolean => getRole() === 'ADMIN';

export const canAcknowledge = (): boolean =>
  ['ADMIN', 'TECHNICIAN'].includes(getRole());

/** firstName/lastName are stored separately (not in JWT) */
export const getFirstName = (): string =>
  localStorage.getItem('firstName') || '';

export const getLastName = (): string =>
  localStorage.getItem('lastName') || '';

export const getFullName = (): string => {
  const first = getFirstName();
  const last = getLastName();
  return (first || last) ? `${first} ${last}`.trim() : 'User';
};

export const getTenantName = (): string =>
  localStorage.getItem('tenantName') || '';
