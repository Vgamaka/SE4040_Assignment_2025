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
    localStorage.setItem("token", data.token);
    localStorage.setItem("role", data.role);
    localStorage.setItem("username", data.username);
    setToken(data.token); setRole(data.role); setUsername(data.username);
  };

  const logout = () => {
    localStorage.clear();
    setToken(null); setRole(null); setUsername(null);
  };

  return (
    <AuthContext.Provider value={{ token, role, username, login, logout, isAuthed: !!token }}>
      {children}
    </AuthContext.Provider>
  );
}
