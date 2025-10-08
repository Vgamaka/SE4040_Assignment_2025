import { BrowserRouter as Router, Routes, Route } from "react-router-dom";
import Login from "./pages/Login.jsx";
import AdminDashboard from "./pages/AdminDashboard.jsx";
import StationDashboard from "./pages/StationDashboard.jsx";
import BackOfficeDashboard from "./pages/BackOfficeDashboard.jsx";
import ProtectedRoute from "./components/ProtectedRoute.jsx";

export default function App() {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<Login />} />
        <Route path="/login" element={<Login />} />

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
      </Routes>
    </Router>
  );
}
