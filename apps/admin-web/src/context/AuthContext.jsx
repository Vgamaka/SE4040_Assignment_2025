import { createContext, useContext, useState, useEffect } from "react";
import { login as apiLogin, logout as apiLogout, getUser, getToken } from "../services/api";

const AuthContext = createContext(null);
export const useAuth = () => useContext(AuthContext);

export function AuthProvider({ children }) {
  // hydrate from storage that services/api.js manages
  const [token, setToken] = useState(getToken());
  const [user, setUser] = useState(getUser());

  // derived for backward-compat with pages using username/role
  const username = user?.fullName || user?.nic || "";
  const role = Array.isArray(user?.roles) && user.roles.length ? user.roles[0] : null;

  // keep local state in sync if other tabs change storage
  useEffect(() => {
    const onStorage = () => {
      setToken(getToken());
      setUser(getUser());
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const login = async (username, password) => {
    // unified login: username (NIC or email), password
    const data = await apiLogin({ username, password });
    // apiLogin persists token + user for us; just reflect to state
    setToken(data?.accessToken || getToken());
    setUser(data || getUser());
    // return primary role for caller navigation convenience
    return Array.isArray(data?.roles) && data.roles.length ? data.roles[0] : null;
  };

  const logout = () => {
    apiLogout(); // clears storage and hard-redirects to "/"
  };

  return (
    <AuthContext.Provider
      value={{
        token,
        role,
        username,
        user,
        login,
        logout,
        isAuthed: !!token,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
