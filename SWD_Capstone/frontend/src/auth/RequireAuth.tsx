import { Navigate, Outlet, useLocation } from "react-router-dom";
import { getHomePathForRole, useAuth } from "./AuthContext";
import type { UserRole } from "./AuthContext";

type RequireAuthProps = {
  allowedRoles?: UserRole[];
};

export function RequireAuth({ allowedRoles }: RequireAuthProps) {
  const { isAuthenticated, role } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (allowedRoles && (!role || !allowedRoles.includes(role))) {
    return <Navigate to={getHomePathForRole(role)} replace />;
  }

  return <Outlet />;
}
