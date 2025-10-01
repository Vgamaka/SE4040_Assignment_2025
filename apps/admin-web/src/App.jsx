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
      {/* <nav className="bg-white shadow mb-6">
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
      </nav> */}

      <nav className="bg-white border-b border-gray-200">
  <div className="mx-auto max-w-6xl px-6 py-4">
    <div className="flex items-center justify-between">
      {/* Logo/Brand */}
      <div className="flex items-center gap-8">
        <span className="text-xl font-bold bg-gradient-to-r from-blue-600 to-blue-800 bg-clip-text text-transparent">
          EVCharge Admin
        </span>

        {/* Navigation Links */}
        {isAuthed && (
          <div className="flex items-center gap-1">
            <Link 
              to="/" 
              className="px-4 py-2 text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors duration-200"
            >
              Dashboard
            </Link>
            {role === "Backoffice" && (
              <Link 
                to="/users" 
                className="px-4 py-2 text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors duration-200"
              >
                Users
              </Link>
            )}
            <Link 
              to="/stations" 
              className="px-4 py-2 text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors duration-200"
            >
              Stations
            </Link>
            <Link 
              to="/bookings" 
              className="px-4 py-2 text-sm font-medium text-gray-700 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors duration-200"
            >
              Bookings
            </Link>
          </div>
        )}
      </div>

      {/* Right side actions */}
      <div className="flex items-center">
        {isAuthed && (
          <button 
            onClick={logout}
            className="px-4 py-2 text-sm font-medium text-gray-700 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors duration-200"
          >
            Logout
          </button>
        )}
        {!isAuthed && (
          <Link 
            to="/login"
            className="px-5 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors duration-200 shadow-sm"
          >
            Login
          </Link>
        )}
      </div>
    </div>
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
