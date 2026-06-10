import { createContext, useContext, useMemo, useState } from "react";
import { apiClient } from "../api/client";
import type { ReactNode } from "react";

const accessTokenKey = "cpms_access_token";
const refreshTokenKey = "cpms_refresh_token";
const refreshExpiresAtKey = "cpms_refresh_expires_at";
const loginCodeKey = "cpms_login_code";

type TokenResponse = {
  accessToken: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
};

export type UserRole = "Student" | "Lecturer" | "TrainingDepartment" | "SystemAdministrator";

type AuthContextValue = {
  isAuthenticated: boolean;
  loginCode: string | null;
  role: UserRole | null;
  signIn: (loginCode: string, password: string) => Promise<UserRole | null>;
  signInWithGoogle: (idToken: string) => Promise<UserRole | null>;
  signOut: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [loginCode, setLoginCode] = useState<string | null>(() => sessionStorage.getItem(loginCodeKey));
  const [isAuthenticated, setIsAuthenticated] = useState(() => Boolean(sessionStorage.getItem(accessTokenKey)));
  const [role, setRole] = useState<UserRole | null>(() => getRoleFromToken(sessionStorage.getItem(accessTokenKey)));

  const value = useMemo<AuthContextValue>(() => ({
    isAuthenticated,
    loginCode,
    role,
    signIn: async (nextLoginCode, password) => {
      const response = await apiClient.post<TokenResponse>("/auth/login", {
        username: nextLoginCode.trim(),
        password,
      });

      sessionStorage.setItem(loginCodeKey, nextLoginCode.trim());
      setLoginCode(nextLoginCode.trim());
      return applyTokenResponse(response.data, nextLoginCode.trim());
    },
    signInWithGoogle: async (idToken) => {
      const response = await apiClient.post<TokenResponse>("/auth/google", { idToken });
      const nextLoginCode = getLoginCodeFromToken(response.data.accessToken) ?? "Google";
      sessionStorage.setItem(loginCodeKey, nextLoginCode);
      setLoginCode(nextLoginCode);
      return applyTokenResponse(response.data, nextLoginCode);
    },
    signOut: () => {
      sessionStorage.removeItem(accessTokenKey);
      sessionStorage.removeItem(refreshTokenKey);
      sessionStorage.removeItem(refreshExpiresAtKey);
      sessionStorage.removeItem(loginCodeKey);
      setLoginCode(null);
      setRole(null);
      setIsAuthenticated(false);
    },
  }), [isAuthenticated, loginCode, role]);

  function applyTokenResponse(response: TokenResponse, nextLoginCode: string) {
    sessionStorage.setItem(accessTokenKey, response.accessToken);
    sessionStorage.setItem(refreshTokenKey, response.refreshToken);
    sessionStorage.setItem(refreshExpiresAtKey, response.refreshTokenExpiresAt);
    sessionStorage.setItem(loginCodeKey, nextLoginCode);
    const nextRole = getRoleFromToken(response.accessToken);
    setRole(nextRole);
    setIsAuthenticated(true);
    return nextRole;
  }

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function getHomePathForRole(role: UserRole | null) {
  if (role === "SystemAdministrator" || role === "TrainingDepartment") {
    return "/admin";
  }

  return "/";
}

function getRoleFromToken(token: string | null): UserRole | null {
  if (!token) {
    return null;
  }

  try {
    const payload = JSON.parse(atob(token.split(".")[1] ?? "")) as Record<string, unknown>;
    const role = payload.role ?? payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];
    return typeof role === "string" ? role as UserRole : null;
  } catch {
    return null;
  }
}

function getLoginCodeFromToken(token: string | null): string | null {
  if (!token) {
    return null;
  }

  try {
    const payload = JSON.parse(atob(token.split(".")[1] ?? "")) as Record<string, unknown>;
    const name = payload.unique_name ?? payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"];
    return typeof name === "string" ? name : null;
  } catch {
    return null;
  }
}

// Auth state is intentionally colocated with its hook for the small SPA shell.
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }

  return context;
}
