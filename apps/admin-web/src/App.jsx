import { Routes, Route, Navigate } from "react-router-dom";
import Home from "./pages/Home.jsx";
import Login from "./pages/Login.jsx";
import ApplyBackOffice from "./pages/ApplyBackOffice.jsx";
import AdminDashboard from "./pages/AdminDashboard.jsx";
import StationDashboard from "./pages/StationDashboard.jsx";
import BackOfficeDashboard from "./pages/BackOfficeDashboard.jsx";
import ProtectedRoute from "./components/ProtectedRoute.jsx";

export default function App() {
  return (
    <Routes>
      {/* Land on Home instead of Login */}
      <Route path="/" element={<Home />} />

      {/* Public pages */}
      <Route path="/login" element={<Login />} />
      <Route path="/apply-backoffice" element={<ApplyBackOffice />} />

      {/* Protected role-based dashboards */}
      <Route
        path="/dashboard/admin"
        element={
          <ProtectedRoute allowedRoles={["Admin"]}>
            <AdminDashboard />
          </ProtectedRoute>
        }
      />

      <Route
        path="/dashboard/operator"
        element={
          <ProtectedRoute allowedRoles={["Operator"]}>
            <StationDashboard />
          </ProtectedRoute>
        }
      />

      <Route
        path="/dashboard/backoffice"
        element={
          <ProtectedRoute allowedRoles={["BackOffice"]}>
            <BackOfficeDashboard />
          </ProtectedRoute>
        }
      />

      {/* Fallback to Home for unknown routes */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
