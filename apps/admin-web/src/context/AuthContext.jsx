import { createContext, useContext, useState } from "react";
import api from "../services/api";

const AuthContext = createContext(null);
export const useAuth = () => useContext(AuthContext);

export function AuthProvider({ children }) {
  const [token, setToken] = useState(localStorage.getItem("token"));
  const [role, setRole] = useState(localStorage.getItem("role"));
  const [username, setUsername] = useState(localStorage.getItem("username"));
  
  const login = async (username, password, asOwner = false) => {
    const url = asOwner ? "/api/auth/owner/login" : "/api/auth/login";
    const { data } = await api.post(url, { username, password });
    
    // Store individual items for backward compatibility
    localStorage.setItem("token", data.token);
    localStorage.setItem("role", data.role);
    localStorage.setItem("username", data.username);
    
    // Store user object for ProtectedRoute component
    const userData = {
      username: data.username,
      roles: [data.role],
      // Add any other required fields here
    };
    localStorage.setItem("user", JSON.stringify(userData));
    
    setToken(data.token);
    setRole(data.role);
    setUsername(data.username);
    
    return data.role; // Return role for navigation purposes
  };

  const logout = () => {
    localStorage.clear();
    setToken(null);
    setRole(null);
    setUsername(null);
    
    // Force navigation to login page
    window.location.href = "/login";
  };

  return (
    <AuthContext.Provider value={{ token, role, username, login, logout, isAuthed: !!token }}>
      {children}
    </AuthContext.Provider>
  );
}
