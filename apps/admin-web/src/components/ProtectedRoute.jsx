import { Navigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function ProtectedRoute({ children, allowedRoles }) {
  const { token, role } = useAuth();
  
  // Try to get data from both sources for backward compatibility
  const localToken = localStorage.getItem("token");
  const localRole = localStorage.getItem("role");
  const userFromStorage = localStorage.getItem("user");
  
  // Parse user data if it exists
  let parsedUser = null;
  try {
    if (userFromStorage) {
      parsedUser = JSON.parse(userFromStorage);
    }
  } catch (error) {
    console.error("Error parsing user data:", error);
  }
  
  // Check if authenticated
  const isAuthenticated = token || localToken;
  
  if (!isAuthenticated) {
    console.log("Not authenticated, redirecting to login");
    return <Navigate to="/login" replace />;
  }

  // Determine role from available sources
  const effectiveRole = role || localRole || parsedUser?.roles?.[0];
  
  if (!effectiveRole || !allowedRoles.includes(effectiveRole)) {
    console.log("Unauthorized role, redirecting to login");
    return <Navigate to="/login" replace />;
  }

  return children;
}
