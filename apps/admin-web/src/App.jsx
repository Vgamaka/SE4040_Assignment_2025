import { Link, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import ProtectedRoute from "./components/ProtectedRoute";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import Users from "./pages/Users";
import Stations from "./pages/Stations";
import Bookings from "./pages/Bookings";

export default function App() {
  const { isAuthed, role, logout } = useAuth();

  return (
    <div className="min-h-screen bg-gray-50 text-gray-900">
      <nav className="bg-white shadow mb-6">
        <div className="mx-auto max-w-6xl px-4 py-3 flex gap-4 items-center">
          <span className="font-semibold">EVCharge Admin</span>
          {isAuthed && (
            <>
              <Link to="/">Dashboard</Link>
              {role === "Backoffice" && <Link to="/users">Users</Link>}
              <Link to="/stations">Stations</Link>
              <Link to="/bookings">Bookings</Link>
              <button className="ml-auto text-sm" onClick={logout}>Logout</button>
            </>
          )}
          {!isAuthed && <Link className="ml-auto" to="/login">Login</Link>}
        </div>
      </nav>

      <div className="mx-auto max-w-6xl px-4">
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route
            path="/"
            element={
              <ProtectedRoute roles={["Backoffice","StationOperator"]}>
                <Dashboard />
              </ProtectedRoute>
            }
          />
          <Route
            path="/users"
            element={
              <ProtectedRoute roles={["Backoffice"]}>
                <Users />
              </ProtectedRoute>
            }
          />
          <Route
            path="/stations"
            element={
              <ProtectedRoute roles={["Backoffice","StationOperator"]}>
                <Stations />
              </ProtectedRoute>
            }
          />
          <Route
            path="/bookings"
            element={
              <ProtectedRoute roles={["Backoffice","StationOperator"]}>
                <Bookings />
              </ProtectedRoute>
            }
          />
        </Routes>
      </div>
    </div>
  );
}
